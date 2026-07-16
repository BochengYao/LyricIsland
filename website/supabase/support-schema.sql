-- Lyric Island supporter accounts, payment ledger and entitlements.
-- Run this in a separate production Supabase project dedicated to supporter data.
-- No browser-facing RLS policy is created: only its dedicated server-side service role may access it.

create extension if not exists pgcrypto;

create table if not exists public.support_program_config (
  singleton boolean primary key default true check (singleton),
  payment_rewards_enabled boolean not null default false,
  updated_at timestamptz not null default now()
);

insert into public.support_program_config(singleton, payment_rewards_enabled)
values (true, false)
on conflict (singleton) do nothing;

create table if not exists public.support_accounts (
  id uuid primary key default gen_random_uuid(),
  email_hash text not null unique check (char_length(email_hash) = 64),
  email_ciphertext text not null check (char_length(email_ciphertext) between 40 and 600),
  nickname text not null check (char_length(nickname) between 1 and 48),
  public_thanks boolean not null default false,
  email_verified_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

comment on column public.support_accounts.email_verified_at is
  'Confirms control of this mailbox only; it does not prove which Microsoft Store account downloaded the app.';
comment on column public.support_accounts.email_hash is
  'Keyed HMAC-SHA-256 of the normalized email for equality lookup; never use an unkeyed hash here.';
comment on column public.support_accounts.email_ciphertext is
  'Application-layer AES-256-GCM ciphertext. The encryption key must not be stored in this database.';

create table if not exists public.support_email_challenges (
  id uuid primary key default gen_random_uuid(),
  account_id uuid not null references public.support_accounts(id) on delete cascade,
  purpose text not null check (purpose in ('verify_email', 'sign_in')),
  token_hash text not null check (char_length(token_hash) = 64),
  request_ip_hash text check (request_ip_hash is null or char_length(request_ip_hash) = 64),
  expires_at timestamptz not null,
  consumed_at timestamptz,
  failed_attempts integer not null default 0 check (failed_attempts between 0 and 10),
  created_at timestamptz not null default now()
);

create index if not exists support_email_challenges_lookup
  on public.support_email_challenges(account_id, purpose, expires_at desc);

create table if not exists public.support_payments (
  id uuid primary key default gen_random_uuid(),
  account_id uuid not null references public.support_accounts(id) on delete restrict,
  provider text not null check (provider in ('wechat', 'alipay', 'manual')),
  merchant_order_no text not null unique check (char_length(merchant_order_no) between 8 and 80),
  provider_transaction_hash text,
  amount_fen integer not null check (amount_fen between 100 and 10000000),
  currency text not null default 'CNY' check (currency = 'CNY'),
  status text not null default 'pending'
    check (status in ('pending', 'paid', 'refunded', 'closed')),
  verification_source text
    check (verification_source in ('provider_callback', 'manual_admin')),
  verified_at timestamptz,
  paid_at timestamptz,
  refunded_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  check (
    status <> 'paid'
    or (
      verified_at is not null
      and paid_at is not null
      and verification_source is not null
      and provider_transaction_hash is not null
    )
  )
);

create unique index if not exists support_payments_provider_transaction
  on public.support_payments(provider, provider_transaction_hash)
  where provider_transaction_hash is not null;

create index if not exists support_payments_account_status
  on public.support_payments(account_id, status, paid_at desc);

create table if not exists public.support_entitlements (
  id uuid primary key default gen_random_uuid(),
  account_id uuid not null references public.support_accounts(id) on delete cascade,
  entitlement_code text not null
    check (entitlement_code in ('supporter_badge', 'ad_free_lifetime', 'pro_lifetime')),
  grant_source text not null default 'payment_rule'
    check (grant_source in ('payment_rule', 'manual', 'migration')),
  granted_at timestamptz not null default now(),
  revoked_at timestamptz,
  note text,
  unique (account_id, entitlement_code)
);

create index if not exists support_entitlements_active
  on public.support_entitlements(account_id, entitlement_code)
  where revoked_at is null;

create table if not exists public.support_audit_log (
  id uuid primary key default gen_random_uuid(),
  event_type text not null check (char_length(event_type) between 3 and 80),
  account_id uuid references public.support_accounts(id) on delete set null,
  payment_id uuid references public.support_payments(id) on delete set null,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create index if not exists support_audit_log_account
  on public.support_audit_log(account_id, created_at desc);

create or replace function public.set_support_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at := now();
  return new;
end;
$$;

create or replace function public.protect_support_payment_ledger()
returns trigger
language plpgsql
as $$
begin
  if old.account_id <> new.account_id
    or old.provider <> new.provider
    or old.merchant_order_no <> new.merchant_order_no
    or old.amount_fen <> new.amount_fen
    or old.currency <> new.currency then
    raise exception 'Immutable payment identity fields cannot be changed';
  end if;

  if not (
    old.status = new.status
    or (old.status = 'pending' and new.status in ('paid', 'closed'))
    or (old.status = 'paid' and new.status = 'refunded')
  ) then
    raise exception 'Invalid payment status transition: % -> %', old.status, new.status;
  end if;

  return new;
end;
$$;

create or replace function public.validate_support_payment_insert()
returns trigger
language plpgsql
as $$
begin
  if not exists (
    select 1
    from public.support_accounts
    where id = new.account_id
      and email_verified_at is not null
  ) then
    raise exception 'A verified support account is required before payment';
  end if;
  return new;
end;
$$;

drop trigger if exists support_accounts_set_updated_at on public.support_accounts;
create trigger support_accounts_set_updated_at
before update on public.support_accounts
for each row execute function public.set_support_updated_at();

drop trigger if exists support_payments_set_updated_at on public.support_payments;
create trigger support_payments_set_updated_at
before update on public.support_payments
for each row execute function public.set_support_updated_at();

drop trigger if exists support_payments_protect_ledger on public.support_payments;
create trigger support_payments_protect_ledger
before update on public.support_payments
for each row execute function public.protect_support_payment_ledger();

drop trigger if exists support_payments_validate_insert on public.support_payments;
create trigger support_payments_validate_insert
before insert on public.support_payments
for each row execute function public.validate_support_payment_insert();

create or replace function public.issue_support_email_challenge(
  p_account_id uuid,
  p_purpose text,
  p_token_hash text,
  p_request_ip_hash text default null
)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  v_challenge_id uuid;
begin
  if p_purpose not in ('verify_email', 'sign_in')
    or char_length(p_token_hash) <> 64
    or (p_request_ip_hash is not null and char_length(p_request_ip_hash) <> 64) then
    raise exception 'Invalid email challenge request';
  end if;

  if not exists (select 1 from public.support_accounts where id = p_account_id) then
    raise exception 'Support account was not found';
  end if;

  perform pg_advisory_xact_lock(hashtextextended('support-account:' || p_account_id::text, 0));
  if p_request_ip_hash is not null then
    perform pg_advisory_xact_lock(hashtextextended('support-ip:' || p_request_ip_hash, 0));
  end if;

  if (
    select count(*)
    from public.support_email_challenges
    where account_id = p_account_id
      and created_at > now() - interval '15 minutes'
  ) >= 3 then
    raise exception 'Email challenge rate limit exceeded';
  end if;

  if p_request_ip_hash is not null and (
    select count(*)
    from public.support_email_challenges
    where request_ip_hash = p_request_ip_hash
      and created_at > now() - interval '15 minutes'
  ) >= 10 then
    raise exception 'Email challenge rate limit exceeded';
  end if;

  update public.support_email_challenges
  set consumed_at = now()
  where account_id = p_account_id
    and purpose = p_purpose
    and consumed_at is null;

  insert into public.support_email_challenges(
    account_id,
    purpose,
    token_hash,
    request_ip_hash,
    expires_at
  )
  values (
    p_account_id,
    p_purpose,
    p_token_hash,
    p_request_ip_hash,
    now() + interval '10 minutes'
  )
  returning id into v_challenge_id;

  return v_challenge_id;
end;
$$;

create or replace function public.consume_support_email_challenge(
  p_account_id uuid,
  p_purpose text,
  p_token_hash text
)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
  v_challenge public.support_email_challenges%rowtype;
begin
  if p_purpose not in ('verify_email', 'sign_in') or char_length(p_token_hash) <> 64 then
    return false;
  end if;

  select *
  into v_challenge
  from public.support_email_challenges
  where account_id = p_account_id
    and purpose = p_purpose
    and consumed_at is null
    and expires_at > now()
  order by created_at desc
  limit 1
  for update;

  if not found or v_challenge.failed_attempts >= 10 then
    return false;
  end if;

  if v_challenge.token_hash <> p_token_hash then
    update public.support_email_challenges
    set failed_attempts = failed_attempts + 1,
        consumed_at = case when failed_attempts + 1 >= 10 then now() else null end
    where id = v_challenge.id;
    return false;
  end if;

  update public.support_email_challenges
  set consumed_at = now()
  where id = v_challenge.id;

  if p_purpose = 'verify_email' then
    update public.support_accounts
    set email_verified_at = coalesce(email_verified_at, now())
    where id = p_account_id;
  end if;

  insert into public.support_audit_log(event_type, account_id, metadata)
  values ('email_challenge_consumed', p_account_id, jsonb_build_object('purpose', p_purpose));
  return true;
end;
$$;

create or replace function public.reconcile_support_entitlements(p_account_id uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_rewards_enabled boolean;
  v_max_single_payment integer;
begin
  select payment_rewards_enabled
  into v_rewards_enabled
  from public.support_program_config
  where singleton = true;

  if coalesce(v_rewards_enabled, false) then
    select coalesce(max(amount_fen), 0)
    into v_max_single_payment
    from public.support_payments
    where account_id = p_account_id
      and status = 'paid'
      and verified_at is not null;
  else
    v_max_single_payment := 0;
  end if;

  if v_max_single_payment >= 100 then
    insert into public.support_entitlements(account_id, entitlement_code, grant_source)
    values (p_account_id, 'supporter_badge', 'payment_rule')
    on conflict (account_id, entitlement_code) do update
      set grant_source = 'payment_rule', granted_at = now(), revoked_at = null;
  else
    update public.support_entitlements
    set revoked_at = coalesce(revoked_at, now())
    where account_id = p_account_id
      and entitlement_code = 'supporter_badge'
      and grant_source = 'payment_rule'
      and revoked_at is null;
  end if;

  if v_max_single_payment >= 300 then
    insert into public.support_entitlements(account_id, entitlement_code, grant_source)
    values (p_account_id, 'ad_free_lifetime', 'payment_rule')
    on conflict (account_id, entitlement_code) do update
      set grant_source = 'payment_rule', granted_at = now(), revoked_at = null;
  else
    update public.support_entitlements
    set revoked_at = coalesce(revoked_at, now())
    where account_id = p_account_id
      and entitlement_code = 'ad_free_lifetime'
      and grant_source = 'payment_rule'
      and revoked_at is null;
  end if;

  if v_max_single_payment >= 500 then
    insert into public.support_entitlements(account_id, entitlement_code, grant_source)
    values (p_account_id, 'pro_lifetime', 'payment_rule')
    on conflict (account_id, entitlement_code) do update
      set grant_source = 'payment_rule', granted_at = now(), revoked_at = null;
  else
    update public.support_entitlements
    set revoked_at = coalesce(revoked_at, now())
    where account_id = p_account_id
      and entitlement_code = 'pro_lifetime'
      and grant_source = 'payment_rule'
      and revoked_at is null;
  end if;
end;
$$;

create or replace function public.reconcile_all_support_entitlements()
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_account record;
begin
  for v_account in select id from public.support_accounts loop
    perform public.reconcile_support_entitlements(v_account.id);
  end loop;
end;
$$;

create or replace function public.on_support_payment_changed()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
  v_account_id uuid;
  v_payment_id uuid;
  v_status text;
  v_amount_fen integer;
begin
  if tg_op = 'DELETE' then
    v_account_id := old.account_id;
    v_payment_id := old.id;
    v_status := old.status;
    v_amount_fen := old.amount_fen;
  else
    v_account_id := new.account_id;
    v_payment_id := new.id;
    v_status := new.status;
    v_amount_fen := new.amount_fen;
  end if;

  insert into public.support_audit_log(event_type, account_id, payment_id, metadata)
  values (
    'payment_' || lower(tg_op),
    v_account_id,
    case when tg_op = 'DELETE' then null else v_payment_id end,
    jsonb_build_object('status', v_status, 'amount_fen', v_amount_fen)
  );

  perform public.reconcile_support_entitlements(v_account_id);
  if tg_op = 'DELETE' then
    return old;
  end if;
  return new;
end;
$$;

drop trigger if exists support_payment_changed on public.support_payments;
create trigger support_payment_changed
after insert or update or delete on public.support_payments
for each row execute function public.on_support_payment_changed();

alter table public.support_program_config enable row level security;
alter table public.support_accounts enable row level security;
alter table public.support_email_challenges enable row level security;
alter table public.support_payments enable row level security;
alter table public.support_entitlements enable row level security;
alter table public.support_audit_log enable row level security;

alter table public.support_program_config force row level security;
alter table public.support_accounts force row level security;
alter table public.support_email_challenges force row level security;
alter table public.support_payments force row level security;
alter table public.support_entitlements force row level security;
alter table public.support_audit_log force row level security;

revoke all on table public.support_program_config from public, anon, authenticated;
revoke all on table public.support_accounts from public, anon, authenticated;
revoke all on table public.support_email_challenges from public, anon, authenticated;
revoke all on table public.support_payments from public, anon, authenticated;
revoke all on table public.support_entitlements from public, anon, authenticated;
revoke all on table public.support_audit_log from public, anon, authenticated;
revoke all on table public.support_program_config from service_role;
revoke all on table public.support_accounts from service_role;
revoke all on table public.support_email_challenges from service_role;
revoke all on table public.support_payments from service_role;
revoke all on table public.support_entitlements from service_role;
revoke all on table public.support_audit_log from service_role;
revoke all on function public.reconcile_support_entitlements(uuid) from public, anon, authenticated;
revoke all on function public.reconcile_all_support_entitlements() from public, anon, authenticated;
revoke all on function public.set_support_updated_at() from public, anon, authenticated;
revoke all on function public.protect_support_payment_ledger() from public, anon, authenticated;
revoke all on function public.validate_support_payment_insert() from public, anon, authenticated;
revoke all on function public.on_support_payment_changed() from public, anon, authenticated;
revoke all on function public.issue_support_email_challenge(uuid, text, text, text) from public, anon, authenticated;
revoke all on function public.consume_support_email_challenge(uuid, text, text) from public, anon, authenticated;

grant select on table public.support_program_config to service_role;
grant select, insert, update on table public.support_accounts to service_role;
grant select, insert, update on table public.support_payments to service_role;
grant select, insert, update on table public.support_entitlements to service_role;
grant select on table public.support_audit_log to service_role;
grant execute on function public.reconcile_support_entitlements(uuid) to service_role;
grant execute on function public.reconcile_all_support_entitlements() to service_role;
grant execute on function public.issue_support_email_challenge(uuid, text, text, text) to service_role;
grant execute on function public.consume_support_email_challenge(uuid, text, text) to service_role;

-- Keep rewards disabled in the Microsoft Store build until the Store-policy path
-- has been confirmed. To enable the prepared rules later, use the server role:
-- update public.support_program_config set payment_rewards_enabled = true where singleton = true;
-- select public.reconcile_all_support_entitlements();
