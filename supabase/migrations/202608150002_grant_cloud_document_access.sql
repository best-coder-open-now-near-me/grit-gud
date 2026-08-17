-- RLS policies decide which rows an authenticated user may access. These
-- grants permit that role to reach the cloud-document tables in the first
-- place; they intentionally do not grant access to anon or public.

grant select, insert, update, delete
on table public.level_drafts
to authenticated;

grant select, insert, update, delete
on table public.character_documents
to authenticated;
