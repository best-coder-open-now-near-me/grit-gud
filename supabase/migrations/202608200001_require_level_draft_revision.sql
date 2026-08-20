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
    if expected_revision is null
       or current_draft.revision is distinct from expected_revision then
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
