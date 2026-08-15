create or replace function public.list_level_drafts()
returns table(slot text, updated_at timestamptz)
language sql stable security invoker set search_path = '' as $$
  select level_drafts.slot, level_drafts.updated_at
  from public.level_drafts where owner_id = (select auth.uid())
  order by level_drafts.updated_at desc, level_drafts.slot;
$$;

create or replace function public.rename_level_draft(old_slot text, new_slot text)
returns void language plpgsql security invoker set search_path = '' as $$
begin
  if new_slot is null or char_length(new_slot) not between 1 and 64 then
    raise exception 'A draft name of 1-64 characters is required.';
  end if;
  update public.level_drafts set slot = new_slot
  where owner_id = (select auth.uid()) and slot = old_slot;
  if not found then raise exception 'The source draft was not found.'; end if;
end;
$$;

grant execute on function public.list_level_drafts() to authenticated;
grant execute on function public.rename_level_draft(text, text) to authenticated;
