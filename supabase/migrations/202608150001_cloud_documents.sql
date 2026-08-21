-- Grit Gud's first cloud persistence slice.
-- Apply with the Supabase CLI or paste into the Dashboard SQL Editor.

create table if not exists public.level_drafts (
    owner_id uuid not null default auth.uid() references auth.users(id) on delete cascade,
    slot text not null check (char_length(slot) between 1 and 64),
    document jsonb not null,
    updated_at timestamptz not null default now(),
    primary key (owner_id, slot)
);

create table if not exists public.character_documents (
    owner_id uuid not null default auth.uid() references auth.users(id) on delete cascade,
    character_id text not null check (char_length(character_id) between 1 and 128),
    document jsonb not null,
    updated_at timestamptz not null default now(),
    primary key (owner_id, character_id)
);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists level_drafts_set_updated_at on public.level_drafts;
create trigger level_drafts_set_updated_at
before update on public.level_drafts
for each row execute function public.set_updated_at();

drop trigger if exists character_documents_set_updated_at on public.character_documents;
create trigger character_documents_set_updated_at
before update on public.character_documents
for each row execute function public.set_updated_at();

alter table public.level_drafts enable row level security;
alter table public.character_documents enable row level security;

create policy "Users manage their own level drafts"
on public.level_drafts
for all to authenticated
using ((select auth.uid()) = owner_id)
with check ((select auth.uid()) = owner_id);

create policy "Users manage their own character documents"
on public.character_documents
for all to authenticated
using ((select auth.uid()) = owner_id)
with check ((select auth.uid()) = owner_id);
