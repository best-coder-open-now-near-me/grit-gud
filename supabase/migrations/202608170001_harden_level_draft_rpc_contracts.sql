-- Return complete authoritative rows from every level-draft RPC. Mutation
-- callers must not need a follow-up read to discover committed state.

drop function if exists public.duplicate_level_draft(uuid, text);
drop function if exists public.rename_level_draft_by_id(uuid, text);
drop function if exists public.save_level_draft(uuid, bigint, jsonb);
drop function if exists public.create_level_draft(text, jsonb);
drop function if exists public.load_level_draft_by_id(uuid);

create or replace function public.load_level_draft_by_id(requested_id uuid)
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    level_id text,
    display_name text,
    schema_version integer,
    document text
)
language sql stable security definer set search_path = '' as $$
    select drafts.id,
           drafts.name,
           drafts.revision,
           drafts.updated_at,
           drafts.document->>'levelId',
           drafts.document->>'displayName',
           coalesce((drafts.document->>'schemaVersion')::integer, 0),
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
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    level_id text,
    display_name text,
    schema_version integer
)
language plpgsql security definer set search_path = '' as $$
declare
    created public.level_drafts;
    generated_id uuid := gen_random_uuid();
    normalized_name text := btrim(requested_name);
begin
    if normalized_name is null
       or char_length(normalized_name) not between 1 and 64 then
        raise exception using
            errcode = '22023',
            message = 'A draft name of 1-64 characters is required.';
    end if;
    if requested_document is null
       or jsonb_typeof(requested_document) <> 'object'
       or coalesce(requested_document->>'levelId', '') = ''
       or coalesce(requested_document->>'schemaVersion', '')
            !~ '^[1-9][0-9]*$'
       or octet_length(requested_document::text) > 2097152 then
        raise exception using
            errcode = '22023',
            message = 'The level document is missing, malformed, or exceeds 2 MiB.';
    end if;

    insert into public.level_drafts (id, owner_id, slot, name, document)
    values (
        generated_id,
        (select auth.uid()),
        generated_id::text,
        normalized_name,
        requested_document)
    returning * into created;

    insert into public.level_draft_revisions (
        draft_id,
        owner_id,
        revision,
        document)
    values (
        created.id,
        created.owner_id,
        created.revision,
        created.document);

    return query
    select created.id,
           created.name,
           created.revision,
           created.updated_at,
           created.document->>'levelId',
           created.document->>'displayName',
           coalesce((created.document->>'schemaVersion')::integer, 0);
end;
$$;

create or replace function public.save_level_draft(
    requested_id uuid,
    expected_revision bigint,
    requested_document jsonb
)
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    level_id text,
    display_name text,
    schema_version integer
)
language plpgsql security definer set search_path = '' as $$
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
        raise exception using
            errcode = 'P0002',
            message = 'The level draft was not found.';
    end if;
    if current_draft.revision <> expected_revision then
        raise exception using
            errcode = '40001',
            message = 'The level draft has changed since it was loaded.';
    end if;
    if requested_document is null
       or jsonb_typeof(requested_document) <> 'object'
       or coalesce(requested_document->>'levelId', '') = ''
       or coalesce(requested_document->>'schemaVersion', '')
            !~ '^[1-9][0-9]*$'
       or octet_length(requested_document::text) > 2097152 then
        raise exception using
            errcode = '22023',
            message = 'The level document is missing, malformed, or exceeds 2 MiB.';
    end if;

    update public.level_drafts as drafts
    set document = requested_document,
        revision = drafts.revision + 1
    where drafts.id = requested_id
    returning drafts.* into current_draft;

    insert into public.level_draft_revisions (
        draft_id,
        owner_id,
        revision,
        document)
    values (
        current_draft.id,
        current_draft.owner_id,
        current_draft.revision,
        current_draft.document);

    return query
    select current_draft.id,
           current_draft.name,
           current_draft.revision,
           current_draft.updated_at,
           current_draft.document->>'levelId',
           current_draft.document->>'displayName',
           coalesce((current_draft.document->>'schemaVersion')::integer, 0);
end;
$$;

create or replace function public.rename_level_draft_by_id(
    requested_id uuid,
    requested_name text
)
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    level_id text,
    display_name text,
    schema_version integer
)
language plpgsql security definer set search_path = '' as $$
declare
    renamed public.level_drafts;
    normalized_name text := btrim(requested_name);
begin
    if normalized_name is null
       or char_length(normalized_name) not between 1 and 64 then
        raise exception using
            errcode = '22023',
            message = 'A draft name of 1-64 characters is required.';
    end if;

    update public.level_drafts as drafts
    set name = normalized_name
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null
    returning drafts.* into renamed;

    if not found then
        raise exception using
            errcode = 'P0002',
            message = 'The level draft was not found.';
    end if;

    return query
    select renamed.id,
           renamed.name,
           renamed.revision,
           renamed.updated_at,
           renamed.document->>'levelId',
           renamed.document->>'displayName',
           coalesce((renamed.document->>'schemaVersion')::integer, 0);
end;
$$;

create or replace function public.duplicate_level_draft(
    requested_id uuid,
    requested_name text
)
returns table(
    draft_id uuid,
    name text,
    revision bigint,
    updated_at timestamptz,
    level_id text,
    display_name text,
    schema_version integer,
    document text
)
language plpgsql security definer set search_path = '' as $$
declare
    source_document jsonb;
begin
    select drafts.document into source_document
    from public.level_drafts as drafts
    where drafts.id = requested_id
      and drafts.owner_id = (select auth.uid())
      and drafts.deleted_at is null;

    if not found then
        raise exception using
            errcode = 'P0002',
            message = 'The source level draft was not found.';
    end if;

    return query
    select created.draft_id,
           created.name,
           created.revision,
           created.updated_at,
           created.level_id,
           created.display_name,
           created.schema_version,
           source_document::text
    from public.create_level_draft(
        requested_name,
        source_document) as created;
end;
$$;

revoke all on function public.load_level_draft_by_id(uuid) from public;
revoke all on function public.create_level_draft(text, jsonb) from public;
revoke all on function public.save_level_draft(uuid, bigint, jsonb) from public;
revoke all on function public.rename_level_draft_by_id(uuid, text) from public;
revoke all on function public.duplicate_level_draft(uuid, text) from public;

grant execute on function public.load_level_draft_by_id(uuid) to authenticated;
grant execute on function public.create_level_draft(text, jsonb) to authenticated;
grant execute on function public.save_level_draft(uuid, bigint, jsonb) to authenticated;
grant execute on function public.rename_level_draft_by_id(uuid, text) to authenticated;
grant execute on function public.duplicate_level_draft(uuid, text) to authenticated;
