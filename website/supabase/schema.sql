create extension if not exists pgcrypto;

create table if not exists public.incentive_submissions (
  id uuid primary key default gen_random_uuid(),
  kind text not null check (kind in ('feature', 'bug')),
  nickname text not null check (char_length(nickname) between 1 and 48),
  email text not null check (char_length(email) between 3 and 180),
  title text not null check (char_length(title) between 4 and 120),
  body text not null check (char_length(body) between 12 and 4000),
  attachments jsonb not null default '[]'::jsonb,
  status text not null default 'pending' check (status in ('pending', 'reviewing', 'accepted', 'declined')),
  reward_status text not null default 'not_eligible' check (reward_status in ('not_eligible', 'pending', 'issued')),
  reviewer_note text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table public.incentive_submissions
  add column if not exists like_count integer not null default 0 check (like_count >= 0);

create table if not exists public.incentive_likes (
  submission_id uuid not null references public.incentive_submissions(id) on delete cascade,
  voter_token_hash text not null check (char_length(voter_token_hash) = 64),
  created_at timestamptz not null default now(),
  primary key (submission_id, voter_token_hash)
);

create index if not exists incentive_likes_voter
  on public.incentive_likes(voter_token_hash, submission_id);

alter table public.incentive_likes enable row level security;

create or replace function public.toggle_incentive_like(
  p_submission_id uuid,
  p_voter_token_hash text
)
returns table(liked boolean, like_count integer)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_liked boolean;
begin
  if char_length(p_voter_token_hash) <> 64 then
    raise exception 'Invalid voter token';
  end if;

  if not exists (
    select 1
    from public.incentive_submissions s
    where s.id = p_submission_id
      and s.kind = 'feature'
      and s.status = 'accepted'
  ) then
    raise exception 'Suggestion is not available for likes';
  end if;

  if exists (
    select 1
    from public.incentive_likes l
    where l.submission_id = p_submission_id
      and l.voter_token_hash = p_voter_token_hash
  ) then
    delete from public.incentive_likes l
    where l.submission_id = p_submission_id
      and l.voter_token_hash = p_voter_token_hash;

    update public.incentive_submissions s
    set like_count = greatest(0, s.like_count - 1),
        updated_at = now()
    where s.id = p_submission_id;
    v_liked := false;
  else
    insert into public.incentive_likes(submission_id, voter_token_hash)
    values (p_submission_id, p_voter_token_hash);

    update public.incentive_submissions s
    set like_count = s.like_count + 1,
        updated_at = now()
    where s.id = p_submission_id;
    v_liked := true;
  end if;

  return query
  select v_liked, s.like_count
  from public.incentive_submissions s
  where s.id = p_submission_id;
end;
$$;

revoke all on function public.toggle_incentive_like(uuid, text) from public;
revoke all on function public.toggle_incentive_like(uuid, text) from anon;
revoke all on function public.toggle_incentive_like(uuid, text) from authenticated;
grant execute on function public.toggle_incentive_like(uuid, text) to service_role;

create index if not exists incentive_submissions_review_queue
  on public.incentive_submissions (status, kind, created_at desc);

create index if not exists incentive_submissions_public_accepted
  on public.incentive_submissions (kind, status, updated_at desc);

create table if not exists public.release_previews (
  id uuid primary key default gen_random_uuid(),
  version text not null check (char_length(version) between 1 and 40),
  title_zh text not null check (char_length(title_zh) between 1 and 160),
  title_en text not null default '',
  body_zh text not null check (char_length(body_zh) between 1 and 2400),
  body_en text not null default '',
  highlights_zh jsonb not null default '[]'::jsonb,
  highlights_en jsonb not null default '[]'::jsonb,
  target_date date,
  status text not null default 'draft' check (status in ('draft', 'published')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  published_at timestamptz
);

create index if not exists release_previews_public
  on public.release_previews (status, published_at desc);

alter table public.incentive_submissions enable row level security;
alter table public.release_previews enable row level security;

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
  'lyric-island-submissions',
  'lyric-island-submissions',
  false,
  15728640,
  array['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'video/mp4', 'video/webm', 'video/quicktime']
)
on conflict (id) do update set
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

-- No anonymous table or storage policies are created. The website route handlers
-- use the server-only service role, and the browser never receives that key.
