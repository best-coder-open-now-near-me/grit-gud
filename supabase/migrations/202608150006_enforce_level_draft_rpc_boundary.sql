-- The v2 functions enforce identity, ownership, document shape, and revision
-- conflicts. Make them the only mutation boundary for level drafts.

alter function public.list_level_draft_library() security definer;
alter function public.load_level_draft_by_id(uuid) security definer;
alter function public.create_level_draft(text, jsonb) security definer;
alter function public.save_level_draft(uuid, bigint, jsonb) security definer;
alter function public.rename_level_draft_by_id(uuid, text) security definer;
alter function public.duplicate_level_draft(uuid, text) security definer;
alter function public.archive_level_draft(uuid) security definer;

revoke all on table public.level_drafts from authenticated;
revoke all on table public.level_draft_revisions from authenticated;

drop function if exists public.load_level_draft(text);
drop function if exists public.list_level_drafts();
drop function if exists public.rename_level_draft(text, text);

grant execute on function public.list_level_draft_library() to authenticated;
grant execute on function public.load_level_draft_by_id(uuid) to authenticated;
grant execute on function public.create_level_draft(text, jsonb) to authenticated;
grant execute on function public.save_level_draft(uuid, bigint, jsonb) to authenticated;
grant execute on function public.rename_level_draft_by_id(uuid, text) to authenticated;
grant execute on function public.duplicate_level_draft(uuid, text) to authenticated;
grant execute on function public.archive_level_draft(uuid) to authenticated;
