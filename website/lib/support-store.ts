import type {
  SupportAccount,
  SupportEntitlement,
  SupportPayment,
  SupportPaymentProvider,
  SupportVerificationSource
} from "@/data/support-types";
import {
  createCipheriv,
  createDecipheriv,
  createHmac,
  randomBytes,
  randomInt
} from "node:crypto";

function getConfig() {
  const url = process.env.SUPPORT_SUPABASE_URL?.replace(/\/$/, "");
  const key = process.env.SUPPORT_SUPABASE_SERVICE_ROLE_KEY;
  if (!url || !key) throw new Error("Supporter storage is not configured");
  return { url, key };
}

function decodeSecret(name: string, expectedLength?: number) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is not configured`);
  const bytes = Buffer.from(value, "base64");
  if (expectedLength && bytes.length !== expectedLength) {
    throw new Error(`${name} must decode to exactly ${expectedLength} bytes`);
  }
  if (!expectedLength && bytes.length < 32) {
    throw new Error(`${name} must decode to at least 32 bytes`);
  }
  return bytes;
}

function headers(prefer?: string) {
  const { key } = getConfig();
  return {
    apikey: key,
    Authorization: `Bearer ${key}`,
    "Content-Type": "application/json",
    ...(prefer ? { Prefer: prefer } : {})
  };
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const { url } = getConfig();
  const response = await fetch(`${url}${path}`, {
    ...init,
    headers: { ...headers(), ...(init?.headers ?? {}) },
    cache: "no-store"
  });
  if (!response.ok) {
    throw new Error(`Supporter storage failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function normalizeEmail(email: string) {
  return email.trim().toLowerCase();
}

function keyedHash(purpose: string, value: string) {
  const key = decodeSecret("SUPPORT_LOOKUP_HMAC_KEY_BASE64");
  return createHmac("sha256", key).update(`${purpose}\0${value}`, "utf8").digest("hex");
}

function encryptEmail(email: string) {
  const key = decodeSecret("SUPPORT_DATA_ENCRYPTION_KEY_BASE64", 32);
  const iv = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", key, iv);
  const ciphertext = Buffer.concat([cipher.update(email, "utf8"), cipher.final()]);
  const tag = cipher.getAuthTag();
  return `v1.${iv.toString("base64url")}.${ciphertext.toString("base64url")}.${tag.toString("base64url")}`;
}

function decryptEmail(value: string) {
  const [version, ivText, ciphertextText, tagText] = value.split(".");
  if (version !== "v1" || !ivText || !ciphertextText || !tagText) {
    throw new Error("Unsupported supporter email ciphertext");
  }
  const key = decodeSecret("SUPPORT_DATA_ENCRYPTION_KEY_BASE64", 32);
  const decipher = createDecipheriv("aes-256-gcm", key, Buffer.from(ivText, "base64url"));
  decipher.setAuthTag(Buffer.from(tagText, "base64url"));
  return Buffer.concat([
    decipher.update(Buffer.from(ciphertextText, "base64url")),
    decipher.final()
  ]).toString("utf8");
}

export async function createSupportAccountIfMissing(input: {
  email: string;
  nickname: string;
  publicThanks: boolean;
}) {
  const email = normalizeEmail(input.email);
  const rows = await request<SupportAccount[]>(
    "/rest/v1/support_accounts?on_conflict=email_hash&select=id,nickname,public_thanks,email_verified_at,created_at,updated_at",
    {
      method: "POST",
      headers: headers("resolution=ignore-duplicates,return=representation"),
      body: JSON.stringify({
        email_hash: keyedHash("email", email),
        email_ciphertext: encryptEmail(email),
        nickname: input.nickname.trim(),
        public_thanks: input.publicThanks
      })
    }
  );
  if (rows[0]) return rows[0];

  const existing = await request<SupportAccount[]>(
    `/rest/v1/support_accounts?select=id,nickname,public_thanks,email_verified_at,created_at,updated_at&email_hash=eq.${keyedHash("email", email)}&limit=1`
  );
  if (!existing[0]) throw new Error("Support account creation returned no result");
  return existing[0];
}

export async function updateVerifiedSupportProfile(input: {
  accountId: string;
  nickname: string;
  publicThanks: boolean;
}) {
  const rows = await request<SupportAccount[]>(
    `/rest/v1/support_accounts?select=id,nickname,public_thanks,email_verified_at,created_at,updated_at&id=eq.${encodeURIComponent(input.accountId)}&email_verified_at=not.is.null`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        nickname: input.nickname.trim(),
        public_thanks: input.publicThanks
      })
    }
  );
  if (!rows[0]) throw new Error("Verified support account was not found");
  return rows[0];
}

export async function getSupportEmailForVerification(accountId: string) {
  const rows = await request<Array<{ email_ciphertext: string }>>(
    `/rest/v1/support_accounts?select=email_ciphertext&id=eq.${encodeURIComponent(accountId)}&limit=1`
  );
  if (!rows[0]) throw new Error("Support account was not found");
  return decryptEmail(rows[0].email_ciphertext);
}

export async function issueEmailVerificationChallenge(input: {
  accountId: string;
  requestIp: string;
}) {
  const code = randomInt(100000, 1000000).toString();
  const tokenHash = keyedHash(`email-code:${input.accountId}:verify_email`, code);
  const challengeId = await request<string>(
    "/rest/v1/rpc/issue_support_email_challenge",
    {
      method: "POST",
      body: JSON.stringify({
        p_account_id: input.accountId,
        p_purpose: "verify_email",
        p_token_hash: tokenHash,
        p_request_ip_hash: keyedHash("request-ip", input.requestIp)
      })
    }
  );
  // The caller must send this code through the configured email provider and
  // must never return it in an HTTP response or write it to logs.
  return { challengeId, code };
}

export async function consumeEmailVerificationChallenge(input: {
  accountId: string;
  code: string;
}) {
  if (!/^\d{6}$/.test(input.code)) return false;
  return request<boolean>("/rest/v1/rpc/consume_support_email_challenge", {
    method: "POST",
    body: JSON.stringify({
      p_account_id: input.accountId,
      p_purpose: "verify_email",
      p_token_hash: keyedHash(
        `email-code:${input.accountId}:verify_email`,
        input.code
      )
    })
  });
}

export async function createPendingSupportPayment(input: {
  accountId: string;
  provider: SupportPaymentProvider;
  merchantOrderNo: string;
  amountFen: number;
}) {
  const rows = await request<SupportPayment[]>("/rest/v1/support_payments", {
    method: "POST",
    headers: headers("return=representation"),
    body: JSON.stringify({
      account_id: input.accountId,
      provider: input.provider,
      merchant_order_no: input.merchantOrderNo,
      amount_fen: input.amountFen,
      status: "pending"
    })
  });
  if (!rows[0]) throw new Error("Support payment creation returned no result");
  return rows[0];
}

// Call this only after the provider callback signature and amount have been verified.
// Never expose this function through a route that trusts a browser or desktop client.
export async function markSupportPaymentPaid(input: {
  provider: SupportPaymentProvider;
  merchantOrderNo: string;
  providerTransactionId: string;
  amountFen: number;
  verificationSource: SupportVerificationSource;
  paidAt: string;
}) {
  const now = new Date().toISOString();
  const rows = await request<SupportPayment[]>(
    `/rest/v1/support_payments?merchant_order_no=eq.${encodeURIComponent(input.merchantOrderNo)}&provider=eq.${input.provider}&amount_fen=eq.${input.amountFen}&status=eq.pending`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({
        provider_transaction_hash: keyedHash(
          `provider-transaction:${input.provider}`,
          input.providerTransactionId
        ),
        status: "paid",
        verification_source: input.verificationSource,
        verified_at: now,
        paid_at: input.paidAt
      })
    }
  );
  if (!rows[0]) throw new Error("Pending support payment was not found");
  return rows[0];
}

export async function markSupportPaymentRefunded(input: {
  merchantOrderNo: string;
  refundedAt: string;
}) {
  const rows = await request<SupportPayment[]>(
    `/rest/v1/support_payments?merchant_order_no=eq.${encodeURIComponent(input.merchantOrderNo)}&status=eq.paid`,
    {
      method: "PATCH",
      headers: headers("return=representation"),
      body: JSON.stringify({ status: "refunded", refunded_at: input.refundedAt })
    }
  );
  if (!rows[0]) throw new Error("Paid support payment was not found");
  return rows[0];
}

export async function getActiveSupportEntitlements(accountId: string) {
  return request<SupportEntitlement[]>(
    `/rest/v1/support_entitlements?select=*&account_id=eq.${encodeURIComponent(accountId)}&revoked_at=is.null&order=granted_at.asc`
  );
}
