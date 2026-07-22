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

export async function recordAccessEvent(request: Request, input: AccessEventInput) {
  const country = request.headers.get("x-vercel-ip-country") || request.headers.get("cf-ipcountry");
  await query<unknown>("/rest/v1/access_logs", {
    method: "POST",
    headers: headers("return=minimal"),
    body: JSON.stringify({
      scope: input.scope,
      event_type: input.eventType.slice(0, 80),
      path: cleanPath(input.path, request),
      method: (input.method || request.method).slice(0, 12),
      status_code: input.statusCode ?? null,
      visitor_hash: await visitorHash(request),
      country: country?.slice(0, 8) || null,
      user_agent: request.headers.get("user-agent")?.slice(0, 500) || null,
      referrer: input.referrer?.slice(0, 800) || request.headers.get("referer")?.slice(0, 800) || null,
      severity: input.severity ?? "normal",
      details: input.details ?? {}
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
  const logs = await query<AccessLogEntry[]>(
    "/rest/v1/access_logs?select=*&order=created_at.desc&limit=300"
  );
  return {
    logs,
    unreadAlerts: logs.filter(
      (item) => item.severity !== "normal" && !item.acknowledged_at
    ).length
  };
}

export async function acknowledgeAccessAlerts() {
  await query<unknown>(
    "/rest/v1/access_logs?severity=in.(warning,critical)&acknowledged_at=is.null",
    {
      method: "PATCH",
      headers: headers("return=minimal"),
      body: JSON.stringify({ acknowledged_at: new Date().toISOString() })
    }
  );
}
