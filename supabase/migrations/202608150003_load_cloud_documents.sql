create or replace function public.load_level_draft(requested_slot text)
returns table(document text)
language sql
stable
security invoker
set search_path = ''
as $$
    select level_drafts.document::text
    from public.level_drafts
    where owner_id = (select auth.uid()) and slot = requested_slot;
$$;

create or replace function public.load_character_document(requested_character_id text)
returns table(document text)
language sql
stable
security invoker
set search_path = ''
as $$
    select character_documents.document::text
    from public.character_documents
    where owner_id = (select auth.uid()) and character_id = requested_character_id;
$$;

grant execute on function public.load_level_draft(text) to authenticated;
grant execute on function public.load_character_document(text) to authenticated;
