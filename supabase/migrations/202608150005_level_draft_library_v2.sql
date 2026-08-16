-- Add the durable multi-draft model without breaking the legacy slot API.

alter table public.level_drafts
    add column if not exists id uuid,
    add column if not exists name text,
    add column if not exists revision bigint not null default 1,
    add column if not exists created_at timestamptz not null default now(),
    add column if not exists deleted_at timestamptz;

update public.level_drafts
set id = gen_random_uuid()
where id is null;

update public.level_drafts
set name = btrim(slot)
where name is null;

alter table public.level_drafts
    alter column id set default gen_random_uuid(),
    alter column id set not null,
    alter column name set not null;

create unique index if not exists level_drafts_id_key
    on public.level_drafts (id);

create unique index if not exists level_drafts_owner_name_key
    on public.level_drafts (owner_id, lower(name))
    where deleted_at is null;

do $$
begin
    if not exists (
        select 1 from pg_constraint
        where conname = 'level_drafts_name_format'
          and conrelid = 'public.level_drafts'::regclass
    ) then
        alter table public.level_drafts add constraint level_drafts_name_format
            check (name = btrim(name) and char_length(name) between 1 and 64);
    end if;
    if not exists (
        select 1 from pg_constraint
        where conname = 'level_drafts_revision_positive'
          and conrelid = 'public.level_drafts'::regclass
    ) then
        alter table public.level_drafts add constraint level_drafts_revision_positive
            check (revision >= 1);
    end if;
    if not exists (
        select 1 from pg_constraint
        where conname = 'level_drafts_document_size'
          and conrelid = 'public.level_drafts'::regclass
    ) then
        alter table public.level_drafts add constraint level_drafts_document_size
            check (octet_length(document::text) <= 2097152);
    end if;
    if not exists (
        select 1 from pg_constraint
        where conname = 'level_drafts_document_identity'
          and conrelid = 'public.level_drafts'::regclass
    ) then
        alter table public.level_drafts add constraint level_drafts_document_identity
            check (
                jsonb_typeof(document) = 'object'
                and coalesce(document->>'levelId', '') <> ''
                and coalesce(document->>'schemaVersion', '') ~ '^[1-9][0-9]*$'
            );
    end if;
end;
$$;

create table if not exists public.level_draft_revisions (
    draft_id uuid not null references public.level_drafts(id) on delete cascade,
    owner_id uuid not null references auth.users(id) on delete cascade,
    revision bigint not null check (revision >= 1),
    document jsonb not null check (octet_length(document::text) <= 2097152),
    saved_at timestamptz not null default now(),
    primary key (draft_id, revision)
);

alter table public.level_draft_revisions enable row level security;

drop policy if exists "Users read their own level draft revisions"
    on public.level_draft_revisions;
create policy "Users read their own level draft revisions"
on public.level_draft_revisions for select to authenticated
using ((select auth.uid()) = owner_id);

drop policy if exists "Users insert their own level draft revisions"
    on public.level_draft_revisions;
create policy "Users insert their own level draft revisions"
on public.level_draft_revisions for insert to authenticated
with check ((select auth.uid()) = owner_id);

grant select, insert on table public.level_draft_revisions to authenticated;

insert into public.level_draft_revisions (draft_id, owner_id, revision, document, saved_at)
select id, owner_id, revision, document, updated_at
from public.level_drafts
on conflict (draft_id, revision) do nothing;

create or replace function public.list_level_draft_library()
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    level_id text,
    display_name text,
    schema_version integer
)
language sql stable security invoker set search_path = '' as $$
    select drafts.id,
           drafts.name,
           drafts.revision,
           drafts.updated_at,
           drafts.document->>'levelId',
           drafts.document->>'displayName',
           coalesce((drafts.document->>'schemaVersion')::integer, 0)
    from public.level_drafts as drafts
    where drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null
    order by drafts.updated_at desc, lower(drafts.name), drafts.id;
$$;

create or replace function public.load_level_draft_by_id(requested_id uuid)
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    document text
)
language sql stable security invoker set search_path = '' as $$
    select drafts.id,
           drafts.name,
           drafts.revision,
           drafts.updated_at,
           drafts.document::text
    from public.level_drafts as drafts
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null;
$$;

create or replace function public.create_level_draft(
    requested_name text,
    requested_document jsonb
)
returns table(draft_id uuid, name text, revision bigint, updated_at timestamptz)
language plpgsql security invoker set search_path = '' as $$
declare
    created public.level_drafts;
    generated_id uuid := gen_random_uuid();
    normalized_name text := btrim(requested_name);
begin
    if normalized_name is null or char_length(normalized_name) not between 1 and 64 then
        raise exception using errcode = '22023', message = 'A draft name of 1-64 characters is required.';
    end if;
    if requested_document is null
       or jsonb_typeof(requested_document) <> 'object'
       or coalesce(requested_document->>'levelId', '') = ''
       or coalesce(requested_document->>'schemaVersion', '') !~ '^[1-9][0-9]*$'
       or octet_length(requested_document::text) > 2097152 then
        raise exception using errcode = '22023', message = 'The level document is missing, malformed, or exceeds 2 MiB.';
    end if;

    insert into public.level_drafts (id, owner_id, slot, name, document)
    values (generated_id, (select auth.uid()), generated_id::text, normalized_name, requested_document)
    returning * into created;

    insert into public.level_draft_revisions (draft_id, owner_id, revision, document)
    values (created.id, created.owner_id, created.revision, created.document);

    return query select created.id, created.name, created.revision, created.updated_at;
end;
$$;

create or replace function public.save_level_draft(
    requested_id uuid,
    expected_revision bigint,
    requested_document jsonb
)
returns table(draft_id uuid, name text, revision bigint, updated_at timestamptz)
language plpgsql security invoker set search_path = '' as $$
declare
    current_draft public.level_drafts;
begin
    select * into current_draft
    from public.level_drafts as drafts
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null
    for update;

    if not found then
        raise exception using errcode = 'P0002', message = 'The level draft was not found.';
    end if;
    if current_draft.revision <> expected_revision then
        raise exception using errcode = '40001', message = 'The level draft has changed since it was loaded.';
    end if;
    if requested_document is null
       or jsonb_typeof(requested_document) <> 'object'
       or coalesce(requested_document->>'levelId', '') = ''
       or coalesce(requested_document->>'schemaVersion', '') !~ '^[1-9][0-9]*$'
       or octet_length(requested_document::text) > 2097152 then
        raise exception using errcode = '22023', message = 'The level document is missing, malformed, or exceeds 2 MiB.';
    end if;

    update public.level_drafts as drafts
    set document = requested_document,
        revision = drafts.revision + 1
    where drafts.id = requested_id
    returning drafts.* into current_draft;

    insert into public.level_draft_revisions (draft_id, owner_id, revision, document)
    values (current_draft.id, current_draft.owner_id, current_draft.revision, current_draft.document);

    return query select current_draft.id, current_draft.name,
                        current_draft.revision, current_draft.updated_at;
end;
$$;

create or replace function public.rename_level_draft_by_id(requested_id uuid, requested_name text)
returns table(draft_id uuid, name text, revision bigint, updated_at timestamptz)
language plpgsql security invoker set search_path = '' as $$
declare
    renamed public.level_drafts;
    normalized_name text := btrim(requested_name);
begin
    if normalized_name is null or char_length(normalized_name) not between 1 and 64 then
        raise exception using errcode = '22023', message = 'A draft name of 1-64 characters is required.';
    end if;

    update public.level_drafts as drafts
    set name = normalized_name
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null
    returning drafts.* into renamed;

    if not found then
        raise exception using errcode = 'P0002', message = 'The level draft was not found.';
    end if;
    return query select renamed.id, renamed.name, renamed.revision, renamed.updated_at;
end;
$$;

create or replace function public.duplicate_level_draft(requested_id uuid, requested_name text)
returns table(draft_id uuid, name text, revision bigint, updated_at timestamptz)
language plpgsql security invoker set search_path = '' as $$
declare
    source_document jsonb;
begin
    select drafts.document into source_document
    from public.level_drafts as drafts
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null;
    if not found then
        raise exception using errcode = 'P0002', message = 'The source level draft was not found.';
    end if;
    return query select * from public.create_level_draft(requested_name, source_document);
end;
$$;

create or replace function public.archive_level_draft(requested_id uuid)
returns void language plpgsql security invoker set search_path = '' as $$
begin
    update public.level_drafts as drafts
    set deleted_at = now()
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null;
    if not found then
        raise exception using errcode = 'P0002', message = 'The level draft was not found.';
    end if;
end;
$$;

revoke all on function public.list_level_draft_library() from public;
revoke all on function public.load_level_draft_by_id(uuid) from public;
revoke all on function public.create_level_draft(text, jsonb) from public;
revoke all on function public.save_level_draft(uuid, bigint, jsonb) from public;
revoke all on function public.rename_level_draft_by_id(uuid, text) from public;
revoke all on function public.duplicate_level_draft(uuid, text) from public;
revoke all on function public.archive_level_draft(uuid) from public;

grant execute on function public.list_level_draft_library() to authenticated;
grant execute on function public.load_level_draft_by_id(uuid) to authenticated;
grant execute on function public.create_level_draft(text, jsonb) to authenticated;
grant execute on function public.save_level_draft(uuid, bigint, jsonb) to authenticated;
grant execute on function public.rename_level_draft_by_id(uuid, text) to authenticated;
grant execute on function public.duplicate_level_draft(uuid, text) to authenticated;
grant execute on function public.archive_level_draft(uuid) to authenticated;
