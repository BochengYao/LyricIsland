import type { AccessLogEntry, AccessSeverity } from "@/data/incentives-types";

type AccessEventInput = {
  scope: "public" | "admin";
  eventType: string;
  path?: string;
  method?: string;
  statusCode?: number;
  severity?: AccessSeverity;
  referrer?: string;
  details?: Record<string, unknown>;
};

export type AccessEventSource = {
  visitor_hash: string;
  country: string | null;
  user_agent: string | null;
  referrer: string | null;
};

type StoredAuditRow = {
  id: string;
  title_zh: string;
  title_en: string;
  body_zh: string;
  body_en: string;
  highlights_zh: unknown;
  created_at: string;
  published_at: string | null;
};

type StoredSubmissionAudit = {
  id: string;
  kind: "feature" | "bug";
  nickname: string;
  title: string;
  reviewer_note: string | null;
  created_at: string;
};

const AUDIT_VERSION = "__AUDIT_LOG_V1__";
const REVIEW_META_PREFIX = "[[lyric-island-review:v1]]";

function config() {
  const url = process.env.SUPABASE_URL?.replace(/\/$/, "");
  const key = process.env.SUPABASE_SERVICE_ROLE_KEY;
  if (!url || !key) throw new Error("Access log storage is not configured");
  return { url, key };
}

function headers(prefer?: string) {
  const { key } = config();
  return {
    apikey: key,
    ...(key.startsWith("sb_") ? {} : { Authorization: `Bearer ${key}` }),
    "Content-Type": "application/json",
    ...(prefer ? { Prefer: prefer } : {})
  };
}

async function query<T>(path: string, init?: RequestInit): Promise<T> {
  const { url } = config();
  const response = await fetch(`${url}${path}`, {
    ...init,
    headers: { ...headers(), ...(init?.headers ?? {}) },
    cache: "no-store"
  });
  if (!response.ok) throw new Error(`Access log request failed (${response.status})`);
  if (response.status === 204) return undefined as T;
  const body = await response.text();
  return (body ? JSON.parse(body) : undefined) as T;
}

function clientAddress(request: Request) {
  return (
    request.headers.get("cf-connecting-ip") ||
    request.headers.get("ali-real-client-ip") ||
    request.headers.get("x-real-ip") ||
    request.headers.get("x-forwarded-for")?.split(",")[0]?.trim() ||
    "unknown"
  ).slice(0, 128);
}

async function visitorHash(request: Request) {
  const secret = process.env.ADMIN_SESSION_SECRET;
  if (!secret) throw new Error("ADMIN_SESSION_SECRET is not configured");
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  const signature = await crypto.subtle.sign(
    "HMAC",
    key,
    encoder.encode(`access-log:${clientAddress(request)}`)
  );
  return Array.from(new Uint8Array(signature), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

function cleanPath(value: string | undefined, request: Request) {
  const fallback = new URL(request.url).pathname;
  if (!value?.startsWith("/")) return fallback.slice(0, 500);
  try {
    return new URL(value, request.url).pathname.slice(0, 500);
  } catch {
    return fallback.slice(0, 500);
  }
}

function countryCode(request: Request) {
  return (
    request.headers.get("client-ip-geo-location") ||
    request.headers.get("x-vercel-ip-country") ||
    request.headers.get("cf-ipcountry") ||
    request.headers.get("x-country-code")
  )?.slice(0, 8) || null;
}

export async function accessEventSource(request: Request): Promise<AccessEventSource> {
  return {
    visitor_hash: await visitorHash(request),
    country: countryCode(request),
    user_agent: request.headers.get("user-agent")?.slice(0, 500) || null,
    referrer: request.headers.get("referer")?.slice(0, 800) || null
  };
}

export async function safeAccessEventSource(request: Request) {
  try {
    return await accessEventSource(request);
  } catch {
    return null;
  }
}

export async function recordAccessEvent(request: Request, input: AccessEventInput) {
  const source = await accessEventSource(request);
  const path = cleanPath(input.path, request);
  const method = (input.method || request.method).slice(0, 12);
  const severity = input.severity ?? "normal";
  await query<unknown>("/rest/v1/release_previews", {
    method: "POST",
    headers: headers("return=minimal"),
    body: JSON.stringify({
      version: AUDIT_VERSION,
      title_zh: input.eventType.slice(0, 80),
      title_en: severity,
      body_zh: path,
      body_en: method,
      highlights_zh: {
        scope: input.scope,
        status_code: input.statusCode ?? null,
        ...source,
        referrer: input.referrer?.slice(0, 800) || source.referrer,
        details: input.details ?? {}
      },
      highlights_en: [],
      target_date: null,
      status: "draft",
      published_at: null
    })
  });
}

export async function safeRecordAccessEvent(request: Request, input: AccessEventInput) {
  try {
    await recordAccessEvent(request, input);
  } catch {
    // Logging must never break the user-facing or admin request.
  }
}

export async function listAccessLogs() {
  const [auditRows, submissions] = await Promise.all([
    query<StoredAuditRow[]>(
      `/rest/v1/release_previews?select=id,title_zh,title_en,body_zh,body_en,highlights_zh,created_at,published_at&version=eq.${encodeURIComponent(AUDIT_VERSION)}&order=created_at.desc&limit=300`
    ),
    query<StoredSubmissionAudit[]>(
      "/rest/v1/incentive_submissions?select=id,kind,nickname,title,reviewer_note,created_at&order=created_at.desc&limit=300"
    )
  ]);
  const storedLogs = auditRows.map(toAccessLog);
  const submissionLogs = submissions.map(toSubmissionLog);
  const logs = [...storedLogs, ...submissionLogs]
    .sort((left, right) => Date.parse(right.created_at) - Date.parse(left.created_at))
    .slice(0, 300);
  return {
    logs,
    unreadAlerts: logs.filter(
      (item) => item.severity !== "normal" && !item.acknowledged_at
    ).length
  };
}

export async function acknowledgeAccessAlerts() {
  const acknowledgedAt = new Date().toISOString();
  await Promise.all(["warning", "critical"].map((severity) =>
    query<unknown>(
      `/rest/v1/release_previews?version=eq.${encodeURIComponent(AUDIT_VERSION)}&title_en=eq.${severity}&published_at=is.null`,
      {
      method: "PATCH",
      headers: headers("return=minimal"),
        body: JSON.stringify({ published_at: acknowledgedAt, updated_at: acknowledgedAt })
      }
    )
  ));
}

function auditDetails(value: unknown) {
  return value && typeof value === "object" ? value as Record<string, unknown> : {};
}

function toAccessLog(row: StoredAuditRow): AccessLogEntry {
  const meta = auditDetails(row.highlights_zh);
  return {
    id: row.id,
    scope: meta.scope === "admin" ? "admin" : "public",
    event_type: row.title_zh,
    path: row.body_zh,
    method: row.body_en,
    status_code: typeof meta.status_code === "number" ? meta.status_code : null,
    visitor_hash: typeof meta.visitor_hash === "string" ? meta.visitor_hash : "未记录",
    country: typeof meta.country === "string" ? meta.country : null,
    user_agent: typeof meta.user_agent === "string" ? meta.user_agent : null,
    referrer: typeof meta.referrer === "string" ? meta.referrer : null,
    severity: row.title_en === "warning" || row.title_en === "critical" ? row.title_en : "normal",
    details: auditDetails(meta.details),
    created_at: row.created_at,
    acknowledged_at: row.published_at
  };
}

function submissionSource(value: string | null) {
  if (!value?.startsWith(REVIEW_META_PREFIX)) return {};
  try {
    const parsed = JSON.parse(value.slice(REVIEW_META_PREFIX.length)) as Record<string, unknown>;
    return auditDetails(parsed.submitted);
  } catch {
    return {};
  }
}

function toSubmissionLog(row: StoredSubmissionAudit): AccessLogEntry {
  const source = submissionSource(row.reviewer_note);
  return {
    id: `submission:${row.id}`,
    scope: "public",
    event_type: "submission_created",
    path: "/api/incentives/submissions",
    method: "POST",
    status_code: 201,
    visitor_hash: typeof source.visitor_hash === "string" ? source.visitor_hash : "未记录",
    country: typeof source.country === "string" ? source.country : null,
    user_agent: typeof source.user_agent === "string" ? source.user_agent : null,
    referrer: typeof source.referrer === "string" ? source.referrer : null,
    severity: "normal",
    details: {
      submissionId: row.id,
      kind: row.kind,
      nickname: row.nickname,
      title: row.title
    },
    created_at: row.created_at,
    acknowledged_at: null
  };
}
