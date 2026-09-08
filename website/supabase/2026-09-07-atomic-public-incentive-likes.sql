begin;

drop function if exists public.toggle_incentive_like(uuid, text);

create function public.toggle_incentive_like(
  p_submission_id uuid,
  p_voter_token_hash text
)
returns table(liked boolean, like_count integer, already_liked boolean)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_inserted integer;
  v_status text;
  v_reviewer_note text;
  v_is_public boolean := false;
begin
  if char_length(p_voter_token_hash) <> 64 then
    raise exception 'Invalid voter token';
  end if;

  select s.status, s.reviewer_note
  into v_status, v_reviewer_note
  from public.incentive_submissions s
  where s.id = p_submission_id
  for update;

  if not found or v_status <> 'accepted' then
    raise exception 'Suggestion is not available for likes';
  end if;

  if left(v_reviewer_note, length('[[lyric-island-review:v1]]')) = '[[lyric-island-review:v1]]' then
    begin
      v_is_public := coalesce(
        (substring(v_reviewer_note from length('[[lyric-island-review:v1]]') + 1)::jsonb) -> 'public' = 'true'::jsonb,
        false
      );
    exception when others then
      v_is_public := false;
    end;
  end if;

  if not v_is_public then
    raise exception 'Suggestion is not available for likes';
  end if;

  insert into public.incentive_likes(submission_id, voter_token_hash)
  values (p_submission_id, p_voter_token_hash)
  on conflict (submission_id, voter_token_hash) do nothing;
  get diagnostics v_inserted = row_count;

  if v_inserted = 1 then
    update public.incentive_submissions s
    set like_count = s.like_count + 1,
        updated_at = now()
    where s.id = p_submission_id;
  end if;

  return query
  select true, s.like_count, v_inserted = 0
  from public.incentive_submissions s
  where s.id = p_submission_id;
end;
$$;

revoke all on function public.toggle_incentive_like(uuid, text) from public;
revoke all on function public.toggle_incentive_like(uuid, text) from anon;
revoke all on function public.toggle_incentive_like(uuid, text) from authenticated;
grant execute on function public.toggle_incentive_like(uuid, text) to service_role;

commit;
