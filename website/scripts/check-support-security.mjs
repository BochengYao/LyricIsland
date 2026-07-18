import { readFile } from "node:fs/promises";

const schema = await readFile(new URL("../supabase/support-schema.sql", import.meta.url), "utf8");
const store = await readFile(new URL("../lib/support-store.ts", import.meta.url), "utf8");

const requirements = [
  ["support rewards default off", /payment_rewards_enabled boolean not null default false/],
  ["email lookup uses a keyed hash column", /email_hash text not null unique/],
  ["email is stored as ciphertext", /email_ciphertext text not null/],
  ["sensitive tables force RLS", /alter table public\.support_payments force row level security/],
  ["anonymous roles are revoked", /revoke all on table public\.support_accounts from public, anon, authenticated/],
  ["service role starts from least privilege", /revoke all on table public\.support_payments from service_role/],
  ["audit log is read-only to the service role", /grant select on table public\.support_audit_log to service_role/],
  ["payment identity is immutable", /Immutable payment identity fields cannot be changed/],
  ["payment requires a verified account", /A verified support account is required before payment/],
  ["email challenge rate limit is serialized", /pg_advisory_xact_lock/],
  ["email challenge attempts are capped", /failed_attempts integer not null default 0 check \(failed_attempts between 0 and 10\)/],
  ["provider transaction ID is not stored in plaintext", /provider_transaction_hash text/]
];

const storeRequirements = [
  ["support data uses a dedicated Supabase project", /SUPPORT_SUPABASE_SERVICE_ROLE_KEY/],
  ["email encryption is authenticated", /createCipheriv\("aes-256-gcm"/],
  ["email lookup uses HMAC", /createHmac\("sha256"/],
  ["callback amount is matched against the pending order", /amount_fen=eq\.\$\{input\.amountFen\}/]
];

const failures = [
  ...requirements.filter(([, pattern]) => !pattern.test(schema)).map(([name]) => name),
  ...storeRequirements.filter(([, pattern]) => !pattern.test(store)).map(([name]) => name)
];

if (/\bemail text\b/.test(schema)) failures.push("schema still contains a plaintext email column");
if (/process\.env\.SUPABASE_SERVICE_ROLE_KEY/.test(store)) {
  failures.push("support store falls back to the general website service role");
}

if (failures.length) {
  throw new Error(`Support security checks failed:\n- ${failures.join("\n- ")}`);
}

console.log("Support security checks passed");
