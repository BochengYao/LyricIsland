// This source is converted into esa-dist/entry.js during deployment.
// Build-time placeholders are injected only into the server-side function artifact.
const CONFIG = Object.freeze({
  supabaseUrl: "__ESA_SUPABASE_URL__",
  supabaseKey: "__ESA_SUPABASE_SERVICE_ROLE_KEY__",
  storageBucket: "__ESA_SUPABASE_STORAGE_BUCKET__",
  adminPassword: "__ESA_ADMIN_PASSWORD__",
  adminSessionSecret: "__ESA_ADMIN_SESSION_SECRET__",
  deepseekApiKey: "__ESA_DEEPSEEK_API_KEY__",
  deepseekBaseUrl: "__ESA_DEEPSEEK_BASE_URL__",
  deepseekModel: "__ESA_DEEPSEEK_MODEL__"
});

const ADMIN_COOKIE = "lyric_island_admin";
const VOTER_COOKIE = "lyric_island_voter";
const SESSION_SECONDS = 60 * 60 * 24 * 7;
const VOTER_SECONDS = 60 * 60 * 24 * 365;
const MAX_FILES = 3;
const MAX_FILE_SIZE = 15 * 1024 * 1024;
const MAX_TOTAL_SIZE = 30 * 1024 * 1024;
const REVIEW_META_PREFIX = "[[lyric-island-review:v1]]";
const FEATURE_CONTENT_VERSION = "__FEATURE_CONTENT_V1__";
const AUDIT_VERSION = "__AUDIT_LOG_V1__";
const DEFAULT_FEATURE_CONTENT = JSON.parse("__ESA_FEATURE_CONTENT_JSON__");
const DEFAULT_RELEASE_PREVIEW = JSON.parse("__ESA_RELEASE_PREVIEW_JSON__");
const ALLOWED_MIME_TYPES = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/gif",
  "video/mp4",
  "video/webm",
  "video/quicktime"
]);

function decodeReviewMeta(value) {
  if (!value || !value.startsWith(REVIEW_META_PREFIX)) {
    return {
      developer_reply: value || null,
      is_flagged: false,
      is_public: false,
      source: null
    };
  }
  try {
    const parsed = JSON.parse(value.slice(REVIEW_META_PREFIX.length));
    const submitted = parsed.submitted && typeof parsed.submitted === "object"
      ? parsed.submitted
      : null;
    return {
      developer_reply: typeof parsed.reply === "string" && parsed.reply ? parsed.reply : null,
      is_flagged: parsed.flagged === true,
      is_public: parsed.public === true,
      source: submitted && typeof submitted.visitor_hash === "string"
          ? {
            visitor_hash: submitted.visitor_hash,
            ip_address: typeof submitted.ip_address === "string" ? submitted.ip_address : null,
            ip_source: typeof submitted.ip_source === "string" ? submitted.ip_source : null,
            country: typeof submitted.country === "string" ? submitted.country : null,
            region: typeof submitted.region === "string" ? submitted.region : null,
            city: typeof submitted.city === "string" ? submitted.city : null,
            user_agent: typeof submitted.user_agent === "string" ? submitted.user_agent : null,
            accept_language: typeof submitted.accept_language === "string" ? submitted.accept_language : null,
            request_id: typeof submitted.request_id === "string" ? submitted.request_id : null,
            forwarded_for: typeof submitted.forwarded_for === "string" ? submitted.forwarded_for : null,
            referrer: typeof submitted.referrer === "string" ? submitted.referrer : null
          }
        : null
    };
  } catch {
    return {
      developer_reply: null,
      is_flagged: false,
      is_public: false,
      source: null
    };
  }
}

function encodeReviewMeta(meta) {
  return `${REVIEW_META_PREFIX}${JSON.stringify({
    reply: meta.developer_reply || "",
    flagged: meta.is_flagged === true,
    public: meta.is_public === true,
    submitted: meta.source || null
  })}`;
}

function toSubmission(row) {
  const { reviewer_note, ...submission } = row;
  const meta = decodeReviewMeta(reviewer_note);
  return {
    ...submission,
    developer_reply: meta.developer_reply,
    is_flagged: meta.is_flagged,
    is_public: meta.is_public
  };
}

function cleanFeatureText(value, max) {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

function cleanFeatureLines(value, maxItems, maxLength) {
  return Array.isArray(value)
    ? value
        .filter((item) => typeof item === "string")
        .map((item) => item.trim().slice(0, maxLength))
        .filter(Boolean)
        .slice(0, maxItems)
    : [];
}

function sanitizeFeatureContent(value) {
  const source = value && typeof value === "object" ? value : {};
  const summary = source.summary && typeof source.summary === "object"
    ? source.summary
    : {};
  const sections = (Array.isArray(source.sections) ? source.sections : [])
    .filter((item) => item && typeof item === "object")
    .slice(0, 30)
    .map((item, index) => {
      const titleZh = cleanFeatureText(item.title_zh, 160);
      const titleEn = cleanFeatureText(item.title_en, 160);
      const bodyZh = cleanFeatureText(item.body_zh, 1200);
      const bodyEn = cleanFeatureText(item.body_en, 1200);
      const itemsZh = cleanFeatureLines(item.items_zh, 12, 240);
      const itemsEn = cleanFeatureLines(item.items_en, 12, 240);
      const itemsZhTw = cleanFeatureLines(item.items_zh_tw, 12, 240);
      const itemsJa = cleanFeatureLines(item.items_ja, 12, 240);
      return {
        id: cleanFeatureText(item.id, 80) || `feature-${String(index + 1).padStart(2, "0")}`,
        title_zh: titleZh,
        title_en: titleEn,
        title_zh_tw: cleanFeatureText(item.title_zh_tw, 160) || titleZh,
        title_ja: cleanFeatureText(item.title_ja, 160) || titleEn || titleZh,
        body_zh: bodyZh,
        body_en: bodyEn,
        body_zh_tw: cleanFeatureText(item.body_zh_tw, 1200) || bodyZh,
        body_ja: cleanFeatureText(item.body_ja, 1200) || bodyEn || bodyZh,
        items_zh: itemsZh,
        items_en: itemsEn,
        items_zh_tw: itemsZhTw.length ? itemsZhTw : itemsZh,
        items_ja: itemsJa.length ? itemsJa : (itemsEn.length ? itemsEn : itemsZh),
        visible: item.visible !== false
      };
    })
    .filter((item) => item.title_zh || item.title_en);
  const summaryLabelZh = cleanFeatureText(summary.label_zh, 80) || DEFAULT_FEATURE_CONTENT.summary.label_zh;
  const summaryLabelEn = cleanFeatureText(summary.label_en, 80) || DEFAULT_FEATURE_CONTENT.summary.label_en;
  const summaryItemsZh = cleanFeatureLines(summary.items_zh, 12, 200);
  const summaryItemsEn = cleanFeatureLines(summary.items_en, 12, 200);
  const summaryItemsZhTw = cleanFeatureLines(summary.items_zh_tw, 12, 200);
  const summaryItemsJa = cleanFeatureLines(summary.items_ja, 12, 200);
  return {
    summary: {
      label_zh: summaryLabelZh,
      label_en: summaryLabelEn,
      label_zh_tw: cleanFeatureText(summary.label_zh_tw, 80) || summaryLabelZh,
      label_ja: cleanFeatureText(summary.label_ja, 80) || summaryLabelEn || summaryLabelZh,
      items_zh: summaryItemsZh,
      items_en: summaryItemsEn,
      items_zh_tw: summaryItemsZhTw.length ? summaryItemsZhTw : summaryItemsZh,
      items_ja: summaryItemsJa.length ? summaryItemsJa : (summaryItemsEn.length ? summaryItemsEn : summaryItemsZh),
      visible: summary.visible !== false
    },
    sections
  };
}

function firstPreviewText(...values) {
  for (const value of values) {
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return "";
}

function firstPreviewLines(...values) {
  for (const value of values) {
    if (!Array.isArray(value)) continue;
    const lines = value.filter((item) => typeof item === "string")
      .map((item) => item.trim())
      .filter(Boolean);
    if (lines.length) return lines;
  }
  return [];
}

function normalizeReleasePreview(preview) {
  const titleZh = firstPreviewText(preview.title_zh);
  const titleEn = firstPreviewText(preview.title_en, titleZh);
  const bodyZh = firstPreviewText(preview.body_zh);
  const bodyEn = firstPreviewText(preview.body_en, bodyZh);
  const highlightsZh = firstPreviewLines(preview.highlights_zh);
  const highlightsEn = firstPreviewLines(preview.highlights_en, highlightsZh);
  return {
    ...preview,
    title_zh: titleZh,
    title_en: titleEn,
    title_zh_tw: firstPreviewText(preview.title_zh_tw, titleZh),
    title_ja: firstPreviewText(preview.title_ja, titleEn, titleZh),
    body_zh: bodyZh,
    body_en: bodyEn,
    body_zh_tw: firstPreviewText(preview.body_zh_tw, bodyZh),
    body_ja: firstPreviewText(preview.body_ja, bodyEn, bodyZh),
    highlights_zh: highlightsZh,
    highlights_en: highlightsEn,
    highlights_zh_tw: firstPreviewLines(preview.highlights_zh_tw, highlightsZh),
    highlights_ja: firstPreviewLines(preview.highlights_ja, highlightsEn, highlightsZh)
  };
}

function json(data, status = 200, extraHeaders = {}) {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff",
      ...extraHeaders
    }
  });
}

function jsonError(message, status = 400) {
  return json({ error: message }, status);
}

function normalizedSupabaseUrl() {
  return CONFIG.supabaseUrl.replace(/\/$/, "");
}

function requireSupabase() {
  if (!CONFIG.supabaseUrl || !CONFIG.supabaseKey) {
    throw new Error("Submission storage is not configured");
  }
}

function supabaseHeaders(prefer) {
  requireSupabase();
  return {
    apikey: CONFIG.supabaseKey,
    // Current sb_secret_ keys are opaque API keys, not JWT bearer tokens.
    // Keep Authorization only for the legacy JWT-based service_role key.
    ...(CONFIG.supabaseKey.startsWith("sb_")
      ? {}
      : { Authorization: `Bearer ${CONFIG.supabaseKey}` }),
    "Content-Type": "application/json",
    ...(prefer ? { Prefer: prefer } : {})
  };
}

async function supabase(path, init = {}) {
  requireSupabase();
  const response = await fetch(`${normalizedSupabaseUrl()}${path}`, {
    ...init,
    headers: {
      ...supabaseHeaders(),
      ...(init.headers || {})
    }
  });
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(`Supabase request failed (${response.status}): ${detail.slice(0, 200)}`);
  }
  if (response.status === 204) return undefined;
  const body = await response.text();
  return body ? JSON.parse(body) : undefined;
}

function readCookie(request, name) {
  const cookie = request.headers.get("cookie") || "";
  const prefix = `${name}=`;
  const part = cookie
    .split(";")
    .map((value) => value.trim())
    .find((value) => value.startsWith(prefix));
  return part ? part.slice(prefix.length) : undefined;
}

function bytesToHex(buffer) {
  return Array.from(new Uint8Array(buffer), (byte) =>
    byte.toString(16).padStart(2, "0")
  ).join("");
}

function constantTimeEqual(left, right) {
  if (left.length !== right.length) return false;
  let mismatch = 0;
  for (let index = 0; index < left.length; index += 1) {
    mismatch |= left.charCodeAt(index) ^ right.charCodeAt(index);
  }
  return mismatch === 0;
}

async function sha256(value) {
  return bytesToHex(
    await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value))
  );
}

function clientAddress(request) {
  const candidates = [
    ["ali-cdn-real-ip", request.headers.get("ali-cdn-real-ip")],
    ["true-client-ip", request.headers.get("true-client-ip")],
    ["cf-connecting-ip", request.headers.get("cf-connecting-ip")],
    ["ali-real-client-ip", request.headers.get("ali-real-client-ip")],
    ["x-real-ip", request.headers.get("x-real-ip")],
    ["x-forwarded-for", (request.headers.get("x-forwarded-for") || "").split(",")[0]]
  ];
  for (const [source, rawValue] of candidates) {
    const value = (rawValue || "").trim();
    if (value && value.length <= 128 && /^[0-9a-f:.]+$/i.test(value)) {
      return { address: value, source };
    }
  }
  return { address: "unknown", source: null };
}

async function accessVisitorHash(request) {
  if (!CONFIG.adminSessionSecret) throw new Error("ADMIN_SESSION_SECRET is not configured");
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(CONFIG.adminSessionSecret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  return bytesToHex(
    await crypto.subtle.sign(
      "HMAC",
      key,
      encoder.encode(`access-log:${clientAddress(request).address}`)
    )
  );
}

function cleanAccessPath(value, request) {
  const fallback = new URL(request.url);
  if (typeof value !== "string" || !value.startsWith("/")) return fallback.pathname.slice(0, 500);
  try {
    const url = new URL(value, request.url);
    for (const [key] of url.searchParams) {
      if (/token|password|secret|code|email|auth|key/i.test(key)) {
        url.searchParams.set(key, "[redacted]");
      }
    }
    return `${url.pathname}${url.search}`.slice(0, 500);
  } catch {
    return fallback.pathname.slice(0, 500);
  }
}

function accessCountry(request) {
  return (
    request.headers.get("ali-ip-country") ||
    request.headers.get("client-ip-geo-location") ||
    request.headers.get("x-vercel-ip-country") ||
    request.headers.get("cf-ipcountry") ||
    request.headers.get("x-country-code") ||
    ""
  ).slice(0, 8) || null;
}

async function accessEventSource(request) {
  const client = clientAddress(request);
  return {
    visitor_hash: await accessVisitorHash(request),
    ip_address: client.address === "unknown" ? null : client.address,
    ip_source: client.source,
    country: accessCountry(request),
    region: (
      request.headers.get("ali-ip-region") ||
      request.headers.get("x-vercel-ip-country-region") ||
      request.headers.get("cf-region") ||
      ""
    ).slice(0, 80) || null,
    city: (
      request.headers.get("ali-ip-city") ||
      request.headers.get("x-vercel-ip-city") ||
      request.headers.get("cf-ipcity") ||
      ""
    ).slice(0, 120) || null,
    user_agent: (request.headers.get("user-agent") || "").slice(0, 500) || null,
    accept_language: (request.headers.get("accept-language") || "").slice(0, 300) || null,
    request_id: (
      request.headers.get("x-request-id") ||
      request.headers.get("x-trace-id") ||
      request.headers.get("eagleeye-traceid") ||
      ""
    ).slice(0, 160) || null,
    forwarded_for: (request.headers.get("x-forwarded-for") || "").slice(0, 500) || null,
    referrer: (request.headers.get("referer") || "").slice(0, 800) || null
  };
}

async function safeAccessEventSource(request) {
  try {
    return await accessEventSource(request);
  } catch {
    return null;
  }
}

async function recordAccessEvent(request, input) {
  const source = await accessEventSource(request);
  const path = cleanAccessPath(input.path, request);
  const method = (input.method || request.method).slice(0, 12);
  const severity = input.severity || "normal";
  await supabase("/rest/v1/release_previews", {
    method: "POST",
    headers: supabaseHeaders("return=minimal"),
    body: JSON.stringify({
      version: AUDIT_VERSION,
      title_zh: input.eventType.slice(0, 80),
      title_en: severity,
      body_zh: path,
      body_en: method,
      highlights_zh: {
        scope: input.scope,
        status_code: input.statusCode == null ? null : input.statusCode,
        ...source,
        referrer: (input.referrer || source.referrer || "").slice(0, 800) || null,
        details: input.details || {}
      },
      highlights_en: [],
      target_date: null,
      status: "draft",
      published_at: null
    })
  });
}

async function safeRecordAccessEvent(request, input) {
  try {
    await recordAccessEvent(request, input);
  } catch {
    // Access logging must not break the request being audited.
  }
}

async function signAdminSession(value) {
  if (!CONFIG.adminSessionSecret || CONFIG.adminSessionSecret.length < 24) {
    throw new Error("ADMIN_SESSION_SECRET is not configured");
  }
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(CONFIG.adminSessionSecret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  return bytesToHex(
    await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(value))
  );
}

async function verifyAdminPassword(password) {
  if (!CONFIG.adminPassword) {
    throw new Error("ADMIN_PASSWORD is not configured");
  }
  const [actual, expected] = await Promise.all([
    sha256(password),
    sha256(CONFIG.adminPassword)
  ]);
  return constantTimeEqual(actual, expected);
}

async function createAdminSession() {
  const expires = Math.floor(Date.now() / 1000) + SESSION_SECONDS;
  const payload = `${expires}.${crypto.randomUUID()}`;
  return `${payload}.${await signAdminSession(payload)}`;
}

async function isAdminRequest(request) {
  const value = readCookie(request, ADMIN_COOKIE);
  if (!value) return false;
  const [expiresText, nonce, signature] = value.split(".");
  if (!expiresText || !nonce || !signature) return false;
  const expires = Number(expiresText);
  if (!Number.isFinite(expires) || expires <= Date.now() / 1000) return false;
  const expected = await signAdminSession(`${expiresText}.${nonce}`);
  return constantTimeEqual(signature, expected);
}

function adminCookie(value) {
  return `${ADMIN_COOKIE}=${value}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=${SESSION_SECONDS}`;
}

function clearAdminCookie() {
  return `${ADMIN_COOKIE}=; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=0`;
}

function voterCookie(value) {
  return `${VOTER_COOKIE}=${value}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=${VOTER_SECONDS}`;
}

function firstForwardedValue(value) {
  return value?.split(",", 1)[0]?.trim() || null;
}

function isSameOrigin(request) {
  const origin = request.headers.get("origin");
  if (!origin) return true;

  try {
    const requestUrl = new URL(request.url);
    const host = firstForwardedValue(request.headers.get("x-forwarded-host"))
      ?? request.headers.get("host")
      ?? requestUrl.host;
    const forwardedProtocol = firstForwardedValue(request.headers.get("x-forwarded-proto"));
    const protocol = forwardedProtocol
      ? `${forwardedProtocol.replace(/:$/, "")}:`
      : requestUrl.protocol;
    const publicOrigin = new URL(`${protocol}//${host}`).origin;

    return new URL(origin).origin === publicOrigin;
  } catch {
    return false;
  }
}

function cleanFileName(name) {
  const extension = name.includes(".") ? `.${name.split(".").pop()}` : "";
  return extension.toLowerCase().replace(/[^.a-z0-9]/g, "").slice(0, 12);
}

async function uploadAttachments(files, submissionId) {
  requireSupabase();
  const attachments = [];
  for (const file of files) {
    const path = `${submissionId}/${crypto.randomUUID()}${cleanFileName(file.name)}`;
    const response = await fetch(
      `${normalizedSupabaseUrl()}/storage/v1/object/${encodeURIComponent(CONFIG.storageBucket)}/${path}`,
      {
        method: "POST",
        headers: {
          apikey: CONFIG.supabaseKey,
          ...(CONFIG.supabaseKey.startsWith("sb_")
            ? {}
            : { Authorization: `Bearer ${CONFIG.supabaseKey}` }),
          "Content-Type": file.type,
          "x-upsert": "false"
        },
        body: file
      }
    );
    if (!response.ok) {
      throw new Error(`Attachment upload failed (${response.status})`);
    }
    attachments.push({
      path,
      name: file.name.slice(0, 180),
      type: file.type,
      size: file.size
    });
  }
  return attachments;
}

async function createSubmission(input) {
  const { source = null, ...submission } = input;
  const rows = await supabase("/rest/v1/incentive_submissions", {
    method: "POST",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify({
      ...submission,
      reward_status: "pending",
      reviewer_note: encodeReviewMeta({
        developer_reply: null,
        is_flagged: false,
        is_public: false,
        source
      })
    })
  });
  return toSubmission(rows[0]);
}

async function createSignedUrls(paths) {
  if (!paths.length) return new Map();
  requireSupabase();
  const response = await fetch(
    `${normalizedSupabaseUrl()}/storage/v1/object/sign/${encodeURIComponent(CONFIG.storageBucket)}`,
    {
      method: "POST",
      headers: supabaseHeaders(),
      body: JSON.stringify({ paths, expiresIn: 3600 })
    }
  );
  if (!response.ok) return new Map();
  const rows = await response.json();
  return new Map(
    rows
      .filter((row) => row && row.path && row.signedURL)
      .map((row) => [
        row.path,
        `${normalizedSupabaseUrl()}/storage/v1${row.signedURL}`
      ])
  );
}

async function getPublicIncentives(voterHash) {
  const suggestionRequest = supabase(
    "/rest/v1/incentive_submissions?select=id,kind,nickname,title,body,created_at,like_count,attachments,reviewer_note,status&order=updated_at.desc&limit=100"
  );
  const likesRequest = voterHash
    ? supabase(
        `/rest/v1/incentive_likes?select=submission_id&voter_token_hash=eq.${encodeURIComponent(voterHash)}&limit=200`
      )
    : Promise.resolve([]);
  const previewRequest = supabase(
    "/rest/v1/release_previews?select=*&status=eq.published&order=published_at.desc&limit=20"
  );
  const [rows, likedRows, previewRows] = await Promise.all([
    suggestionRequest,
    likesRequest,
    previewRequest
  ]);
  const likedIds = new Set(likedRows.map((row) => row.submission_id));
  const publicRows = rows.filter((row) => row.status === "accepted" && decodeReviewMeta(row.reviewer_note).is_public).slice(0, 24);
  const firstAttachments = publicRows
    .map((row) => row.attachments && row.attachments[0])
    .filter(Boolean);
  const signedUrls = await createSignedUrls(firstAttachments.map((item) => item.path));
  const suggestions = publicRows.map(({ attachments, reviewer_note, status: _status, ...suggestion }) => {
    const first = attachments && attachments[0];
    const url = first ? signedUrls.get(first.path) : undefined;
    return {
      ...suggestion,
      developer_reply: decodeReviewMeta(reviewer_note).developer_reply,
      liked: likedIds.has(suggestion.id),
      ...(first && url
        ? { attachment: { name: first.name, type: first.type, url } }
        : {})
    };
  });
  const previews = previewRows.map(normalizeReleasePreview);
  return {
    suggestions,
    previews: previews.length
      ? previews
      : [normalizeReleasePreview({
          id: "default-release-preview-v2-1",
          ...DEFAULT_RELEASE_PREVIEW,
          created_at: "2026-07-29T00:00:00.000Z",
          updated_at: "2026-07-29T00:00:00.000Z",
          published_at: "2026-07-29T00:00:00.000Z"
        })]
  };
}

async function toggleSuggestionLike(submissionId, voterTokenHash) {
  const submissions = await supabase(
    `/rest/v1/incentive_submissions?select=id,like_count,reviewer_note,status&id=eq.${encodeURIComponent(submissionId)}&limit=1`
  );
  const submission = submissions[0];
  if (!submission || submission.status !== "accepted" || !decodeReviewMeta(submission.reviewer_note).is_public) {
    throw new Error("Suggestion is not available for likes");
  }
  const existing = await supabase(
    `/rest/v1/incentive_likes?select=submission_id&submission_id=eq.${encodeURIComponent(submissionId)}&voter_token_hash=eq.${encodeURIComponent(voterTokenHash)}&limit=1`
  );
  if (existing.length > 0) {
    return { liked: true, like_count: submission.like_count, already_liked: true };
  }
  await supabase("/rest/v1/incentive_likes", {
    method: "POST",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify({ submission_id: submissionId, voter_token_hash: voterTokenHash })
  });
  const likeCount = submission.like_count + 1;
  await supabase(`/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(submissionId)}`, {
    method: "PATCH",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify({ like_count: likeCount })
  });
  return { liked: true, like_count: likeCount, already_liked: false };
}

async function listSubmissions() {
  const rows = await supabase(
    "/rest/v1/incentive_submissions?select=*&order=created_at.desc&limit=200"
  );
  const attachments = rows.flatMap((row) => row.attachments || []);
  const signedUrls = await createSignedUrls(attachments.map((item) => item.path));
  return rows.map((storedRow) => {
    const row = toSubmission(storedRow);
    return ({
    ...row,
    attachments: (row.attachments || []).map((attachment) => ({
      ...attachment,
      ...(signedUrls.get(attachment.path)
        ? { signedUrl: signedUrls.get(attachment.path) }
        : {})
    }))
  });});
}

async function updateSubmission(id, changes) {
  const currentRows = await supabase(
    `/rest/v1/incentive_submissions?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
  );
  const current = currentRows[0];
  if (!current) throw new Error("Submission not found");
  const previous = toSubmission(current);
  const currentMeta = decodeReviewMeta(current.reviewer_note);
  const { developer_reply, is_flagged, is_public, ...storedChanges } = changes;
  const effectiveStatus = changes.status || current.status;
  const rows = await supabase(
    `/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: supabaseHeaders("return=representation"),
      body: JSON.stringify({
        ...storedChanges,
        reviewer_note: encodeReviewMeta({
          developer_reply: developer_reply !== undefined ? developer_reply : currentMeta.developer_reply,
          is_flagged: is_flagged !== undefined ? is_flagged : currentMeta.is_flagged,
          is_public: effectiveStatus === "accepted"
            ? (is_public !== undefined ? is_public : currentMeta.is_public)
            : false,
          source: currentMeta.source
        }),
        updated_at: new Date().toISOString()
      })
    }
  );
  return { submission: toSubmission(rows[0]), previous };
}

async function deleteSubmission(id) {
  const currentRows = await supabase(
    `/rest/v1/incentive_submissions?select=*&id=eq.${encodeURIComponent(id)}&limit=1`
  );
  const current = currentRows[0];
  if (!current) throw new Error("Submission not found");
  await Promise.allSettled((current.attachments || []).map((attachment) =>
    fetch(
      `${normalizedSupabaseUrl()}/storage/v1/object/${encodeURIComponent(CONFIG.storageBucket)}/${attachment.path}`,
      {
        method: "DELETE",
        headers: {
          apikey: CONFIG.supabaseKey,
          ...(CONFIG.supabaseKey.startsWith("sb_") ? {} : { Authorization: `Bearer ${CONFIG.supabaseKey}` })
        }
      }
    )
  ));
  await supabase(`/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(id)}`, {
    method: "DELETE",
    headers: supabaseHeaders("return=representation")
  });
  return toSubmission(current);
}

async function listAccessLogs() {
  const [auditRows, submissions] = await Promise.all([
    supabase(
      `/rest/v1/release_previews?select=id,title_zh,title_en,body_zh,body_en,highlights_zh,created_at,published_at&version=eq.${encodeURIComponent(AUDIT_VERSION)}&order=created_at.desc&limit=500`
    ),
    supabase(
      "/rest/v1/incentive_submissions?select=id,kind,nickname,title,body,reviewer_note,created_at&order=created_at.desc&limit=500"
    )
  ]);
  const submissionsById = new Map(submissions.map((submission) => [submission.id, submission]));
  const storedLogs = auditRows.map(toAccessLog).map((log) => {
    const submissionId = typeof log.details.submissionId === "string"
      ? log.details.submissionId
      : "";
    const submission = submissionsById.get(submissionId);
    if (!submission || typeof log.details.submissionTitle === "string") return log;
    return {
      ...log,
      details: {
        ...log.details,
        submissionTitle: submission.title,
        submissionKind: submission.kind,
        legacy: true
      }
    };
  });
  const logs = [
    ...storedLogs,
    ...submissions.map(toSubmissionLog)
  ]
    .sort((left, right) => Date.parse(right.created_at) - Date.parse(left.created_at))
    .slice(0, 500);
  return {
    logs,
    unreadAlerts: logs.filter((item) => item.severity !== "normal" && !item.acknowledged_at).length
  };
}

async function acknowledgeAccessAlerts() {
  const acknowledgedAt = new Date().toISOString();
  await Promise.all(["warning", "critical"].map((severity) =>
    supabase(
      `/rest/v1/release_previews?version=eq.${encodeURIComponent(AUDIT_VERSION)}&title_en=eq.${severity}&published_at=is.null`,
      {
      method: "PATCH",
      headers: supabaseHeaders("return=minimal"),
        body: JSON.stringify({ published_at: acknowledgedAt, updated_at: acknowledgedAt })
      }
    )
  ));
}

function auditDetails(value) {
  return value && typeof value === "object" ? value : {};
}

function toAccessLog(row) {
  const meta = auditDetails(row.highlights_zh);
  return {
    id: row.id,
    scope: meta.scope === "admin" ? "admin" : "public",
    event_type: row.title_zh,
    path: row.body_zh,
    method: row.body_en,
    status_code: typeof meta.status_code === "number" ? meta.status_code : null,
    ip_address: typeof meta.ip_address === "string" ? meta.ip_address : null,
    ip_source: typeof meta.ip_source === "string" ? meta.ip_source : null,
    visitor_hash: typeof meta.visitor_hash === "string" ? meta.visitor_hash : "未记录",
    country: typeof meta.country === "string" ? meta.country : null,
    region: typeof meta.region === "string" ? meta.region : null,
    city: typeof meta.city === "string" ? meta.city : null,
    user_agent: typeof meta.user_agent === "string" ? meta.user_agent : null,
    accept_language: typeof meta.accept_language === "string" ? meta.accept_language : null,
    request_id: typeof meta.request_id === "string" ? meta.request_id : null,
    forwarded_for: typeof meta.forwarded_for === "string" ? meta.forwarded_for : null,
    referrer: typeof meta.referrer === "string" ? meta.referrer : null,
    severity: row.title_en === "warning" || row.title_en === "critical"
      ? row.title_en
      : "normal",
    details: auditDetails(meta.details),
    created_at: row.created_at,
    acknowledged_at: row.published_at
  };
}

function toSubmissionLog(row) {
  const source = decodeReviewMeta(row.reviewer_note).source || {};
  return {
    id: `submission:${row.id}`,
    scope: "public",
    event_type: "submission_created",
    path: "/api/incentives/submissions",
    method: "POST",
    status_code: 201,
    ip_address: typeof source.ip_address === "string" ? source.ip_address : null,
    ip_source: typeof source.ip_source === "string" ? source.ip_source : null,
    visitor_hash: typeof source.visitor_hash === "string" ? source.visitor_hash : "未记录",
    country: typeof source.country === "string" ? source.country : null,
    region: typeof source.region === "string" ? source.region : null,
    city: typeof source.city === "string" ? source.city : null,
    user_agent: typeof source.user_agent === "string" ? source.user_agent : null,
    accept_language: typeof source.accept_language === "string" ? source.accept_language : null,
    request_id: typeof source.request_id === "string" ? source.request_id : null,
    forwarded_for: typeof source.forwarded_for === "string" ? source.forwarded_for : null,
    referrer: typeof source.referrer === "string" ? source.referrer : null,
    severity: "normal",
    details: {
      submissionId: row.id,
      kind: row.kind,
      nickname: row.nickname,
      title: row.title,
      body: row.body
    },
    created_at: row.created_at,
    acknowledged_at: null
  };
}

async function listReleasePreviews() {
  const rows = await supabase(
    "/rest/v1/release_previews?select=*&version=not.in.(__FEATURE_CONTENT_V1__,__AUDIT_LOG_V1__)&order=created_at.desc&limit=50"
  );
  const previews = rows.filter((row) => !row.version.startsWith("__"));
  if (previews.length) return previews.map(normalizeReleasePreview);
  return [await createReleasePreview(DEFAULT_RELEASE_PREVIEW)];
}

async function createReleasePreview(input) {
  const now = new Date().toISOString();
  const rows = await supabase("/rest/v1/release_previews", {
    method: "POST",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify({
      ...input,
      published_at: input.status === "published" ? now : null
    })
  });
  return normalizeReleasePreview(rows[0]);
}

async function updateReleasePreview(id, input) {
  const rows = await supabase(
    `/rest/v1/release_previews?id=eq.${encodeURIComponent(id)}`,
    {
      method: "PATCH",
      headers: supabaseHeaders("return=representation"),
      body: JSON.stringify({
        ...input,
        updated_at: new Date().toISOString(),
        ...(input.status
          ? {
              published_at:
                input.status === "published" ? new Date().toISOString() : null
            }
          : {})
      })
    }
  );
  return normalizeReleasePreview(rows[0]);
}

async function getFeatureContentRow() {
  const rows = await supabase(
    `/rest/v1/release_previews?select=*&version=eq.${encodeURIComponent(FEATURE_CONTENT_VERSION)}&order=updated_at.desc&limit=1`
  );
  return rows[0];
}

function featureContentRowPayload(content) {
  return {
    version: FEATURE_CONTENT_VERSION,
    title_zh: "新功能页内容",
    title_en: "Updates page content",
    body_zh: "由维护者后台管理的新功能页内容。",
    body_en: "Updates page content managed in the maintainer console.",
    highlights_zh: content,
    highlights_en: [],
    target_date: null,
    status: "draft",
    published_at: null
  };
}

async function getFeatureContent() {
  const existing = await getFeatureContentRow();
  if (existing) return sanitizeFeatureContent(existing.highlights_zh);
  const content = sanitizeFeatureContent(DEFAULT_FEATURE_CONTENT);
  const rows = await supabase("/rest/v1/release_previews", {
    method: "POST",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify(featureContentRowPayload(content))
  });
  return sanitizeFeatureContent((rows[0] && rows[0].highlights_zh) || content);
}

async function saveFeatureContent(value) {
  const content = sanitizeFeatureContent(value);
  if (!content.sections.length) throw new Error("At least one feature section is required");
  if (content.sections.some((section) =>
    section.visible &&
    (!section.title_zh || !section.title_en || !section.body_zh || !section.body_en)
  )) {
    throw new Error("Visible feature sections require bilingual titles and descriptions");
  }
  const existing = await getFeatureContentRow();
  if (!existing) {
    const rows = await supabase("/rest/v1/release_previews", {
      method: "POST",
      headers: supabaseHeaders("return=representation"),
      body: JSON.stringify(featureContentRowPayload(content))
    });
    return sanitizeFeatureContent((rows[0] && rows[0].highlights_zh) || content);
  }
  const rows = await supabase(
    `/rest/v1/release_previews?id=eq.${encodeURIComponent(existing.id)}`,
    {
      method: "PATCH",
      headers: supabaseHeaders("return=representation"),
      body: JSON.stringify({
        highlights_zh: content,
        updated_at: new Date().toISOString()
      })
    }
  );
  return sanitizeFeatureContent((rows[0] && rows[0].highlights_zh) || content);
}

function text(form, key, max) {
  const value = form.get(key);
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

function validEmail(value) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) && value.length <= 180;
}

function lines(value) {
  return Array.isArray(value)
    ? value
        .filter((item) => typeof item === "string")
        .map((item) => item.trim())
        .filter(Boolean)
        .slice(0, 12)
    : [];
}

function previewPayload(body) {
  const version = typeof body.version === "string" ? body.version.trim().slice(0, 40) : "";
  const optionalLocalizedBody = (field) => Object.prototype.hasOwnProperty.call(body, field)
    ? { [field]: typeof body[field] === "string" ? body[field].trim().slice(0, 2400) : "" }
    : {};
  return {
    version,
    title_zh: version,
    title_en: version,
    title_zh_tw: version,
    title_ja: version,
    body_zh: typeof body.body_zh === "string" ? body.body_zh.trim().slice(0, 2400) : "",
    body_en: typeof body.body_en === "string" ? body.body_en.trim().slice(0, 2400) : "",
    highlights_zh: lines(body.highlights_zh),
    highlights_en: lines(body.highlights_en),
    ...optionalLocalizedBody("body_zh_tw"),
    ...optionalLocalizedBody("body_ja"),
    target_date:
      typeof body.target_date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(body.target_date)
        ? body.target_date
        : null,
    status: body.status === "published" ? "published" : "draft"
  };
}

async function handlePublic(request) {
  try {
    const token = readCookie(request, VOTER_COOKIE);
    const data = await getPublicIncentives(token ? await sha256(token) : undefined);
    return json({ ...data, configured: true });
  } catch (error) {
    if (error instanceof Error && error.message.includes("not configured")) {
      return json({ suggestions: [], previews: [], configured: false });
    }
    return jsonError("Unable to load community updates", 500);
  }
}

async function handleFeatures() {
  try {
    return json({ content: await getFeatureContent() });
  } catch {
    return jsonError("Unable to load feature content", 500);
  }
}

async function handleSubmission(request) {
  if (!isSameOrigin(request)) return jsonError("Invalid origin", 403);
  try {
    const form = await request.formData();
    if (text(form, "company", 200)) return jsonError("Invalid submission");

    const kind = text(form, "kind", 20);
    const nickname = text(form, "nickname", 48);
    const email = text(form, "email", 180).toLowerCase();
    const title = text(form, "title", 120);
    const body = text(form, "body", 4000);
    if (kind !== "feature" && kind !== "bug") return jsonError("Invalid submission type");
    if (!nickname) return jsonError("Nickname is required");
    if (!validEmail(email)) return jsonError("A valid email is required");
    if (title.length < 4) return jsonError("Please add a more specific title");
    if (body.length < 12) return jsonError("Please add a little more detail");

    const files = form
      .getAll("attachments")
      .filter((value) => typeof File !== "undefined" && value instanceof File && value.size > 0);
    if (files.length > MAX_FILES) return jsonError("Up to 3 attachments are allowed");
    if (files.some((file) => !ALLOWED_MIME_TYPES.has(file.type))) {
      return jsonError("Only JPEG, PNG, WebP, GIF, MP4, WebM or MOV attachments are allowed");
    }
    if (files.some((file) => file.size > MAX_FILE_SIZE)) {
      return jsonError("Each attachment must be 15 MB or smaller");
    }
    if (files.reduce((total, file) => total + file.size, 0) > MAX_TOTAL_SIZE) {
      return jsonError("Attachments must total 30 MB or less");
    }

    const id = crypto.randomUUID();
    const attachments = await uploadAttachments(files, id);
    const submission = await createSubmission({
      id,
      kind,
      nickname,
      email,
      title,
      body,
      attachments,
      source: await safeAccessEventSource(request)
    });
    return json({ id: submission.id, status: submission.status }, 201);
  } catch (error) {
    if (error instanceof Error && error.message.includes("not configured")) {
      return jsonError("Submission service is not configured yet", 503);
    }
    return jsonError("Submission could not be saved. Please try again later.", 500);
  }
}

async function handleLike(request) {
  if (!isSameOrigin(request)) return jsonError("Invalid origin", 403);
  try {
    const body = await request.json();
    const submissionId = typeof body.submissionId === "string" ? body.submissionId : "";
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(submissionId)) {
      return jsonError("Invalid suggestion");
    }
    const existingToken = readCookie(request, VOTER_COOKIE);
    const token = existingToken || crypto.randomUUID();
    const result = await toggleSuggestionLike(submissionId, await sha256(token));
    return json(
      result,
      200,
      existingToken ? {} : { "Set-Cookie": voterCookie(token) }
    );
  } catch (error) {
    if (error instanceof Error && error.message.includes("not configured")) {
      return jsonError("Like service is not configured yet", 503);
    }
    return jsonError("Like could not be saved", 500);
  }
}

async function handleLogin(request) {
  if (!isSameOrigin(request)) {
    await safeRecordAccessEvent(request, { scope: "admin", eventType: "cross_origin_login_attempt", severity: "critical", statusCode: 403 });
    return jsonError("Invalid origin", 403);
  }
  try {
    const body = await request.json();
    const password = typeof body.password === "string" ? body.password : "";
    if (!(await verifyAdminPassword(password))) {
      await safeRecordAccessEvent(request, { scope: "admin", eventType: "login_failed", severity: "warning", statusCode: 401 });
      return jsonError("密码不正确", 401);
    }
    await safeRecordAccessEvent(request, { scope: "admin", eventType: "login_succeeded", statusCode: 200 });
    return json(
      { ok: true },
      200,
      { "Set-Cookie": adminCookie(await createAdminSession()) }
    );
  } catch (error) {
    const unconfigured = error instanceof Error && error.message.includes("not configured");
    return jsonError(unconfigured ? "后台登录尚未配置" : "登录失败", unconfigured ? 503 : 500);
  }
}

function handleLogout(request) {
  if (!isSameOrigin(request)) return jsonError("Invalid origin", 403);
  return json({ ok: true }, 200, { "Set-Cookie": clearAdminCookie() });
}

const AUDITED_SUBMISSION_FIELDS = [
  "kind",
  "nickname",
  "email",
  "title",
  "body",
  "status",
  "reward_status",
  "developer_reply",
  "is_flagged",
  "is_public",
  "like_count",
  "created_at"
];

function changedSubmissionFields(previous, submission) {
  return AUDITED_SUBMISSION_FIELDS
    .filter((field) => previous[field] !== submission[field])
    .map((field) => ({
      field,
      before: previous[field] == null ? null : previous[field],
      after: submission[field] == null ? null : submission[field]
    }));
}

async function handleAdminSubmissions(request) {
  if (!(await isAdminRequest(request))) {
    if (request.method !== "GET") {
      await safeRecordAccessEvent(request, {
        scope: "admin",
        eventType: request.method === "DELETE" ? "unauthorized_submission_delete" : "unauthorized_submission_update",
        severity: isSameOrigin(request) ? "warning" : "critical",
        statusCode: 401
      });
    }
    return jsonError("Unauthorized", 401);
  }
  if (request.method === "GET") {
    try {
      return json({ submissions: await listSubmissions() });
    } catch {
      return jsonError("无法读取提交记录", 500);
    }
  }
  if (request.method === "DELETE") {
    if (!isSameOrigin(request)) {
      await safeRecordAccessEvent(request, { scope: "admin", eventType: "unauthorized_submission_delete", severity: "critical", statusCode: 401 });
      return jsonError("Unauthorized", 401);
    }
    try {
      const body = await request.json();
      const id = typeof body.id === "string" ? body.id : "";
      if (!id) return jsonError("Invalid deletion");
      const deleted = await deleteSubmission(id);
      await safeRecordAccessEvent(request, {
        scope: "admin",
        eventType: "submission_deleted",
        statusCode: 200,
        details: {
          submissionId: id,
          submissionTitle: deleted.title,
          submissionKind: deleted.kind,
          snapshot: {
            title: deleted.title,
            body: deleted.body,
            nickname: deleted.nickname,
            status: deleted.status,
            reward_status: deleted.reward_status,
            developer_reply: deleted.developer_reply,
            like_count: deleted.like_count
          }
        }
      });
      return json({ ok: true });
    } catch {
      return jsonError("删除失败", 500);
    }
  }
  if (request.method !== "PATCH") return jsonError("Method not allowed", 405);
  if (!isSameOrigin(request)) {
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "unauthorized_submission_update",
      severity: "critical",
      statusCode: 401
    });
    return jsonError("Unauthorized", 401);
  }
  try {
    const body = await request.json();
    const statuses = ["pending", "reviewing", "accepted", "declined"];
    const rewards = ["not_eligible", "pending", "issued"];
    const id = typeof body.id === "string" ? body.id : "";
    const kind = body.kind === "feature" || body.kind === "bug" ? body.kind : undefined;
    const nickname = typeof body.nickname === "string" ? body.nickname.trim().slice(0, 48) : undefined;
    const email = typeof body.email === "string" ? body.email.trim().toLowerCase().slice(0, 180) : undefined;
    const title = typeof body.title === "string" ? body.title.trim().slice(0, 120) : undefined;
    const content = typeof body.body === "string" ? body.body.trim().slice(0, 4000) : undefined;
    const status = statuses.includes(body.status) ? body.status : undefined;
    const reward = rewards.includes(body.reward_status) ? body.reward_status : undefined;
    const reply =
      typeof body.developer_reply === "string"
        ? body.developer_reply.trim().slice(0, 2000)
        : undefined;
    const isFlagged = typeof body.is_flagged === "boolean" ? body.is_flagged : undefined;
    const isPublic = typeof body.is_public === "boolean" ? body.is_public : undefined;
    const likeCount = typeof body.like_count === "number" && Number.isInteger(body.like_count) && body.like_count >= 0
      ? body.like_count
      : undefined;
    const createdAt = typeof body.created_at === "string" && !Number.isNaN(Date.parse(body.created_at))
      ? new Date(body.created_at).toISOString()
      : undefined;
    if (!id || (nickname !== undefined && !nickname) || (title !== undefined && title.length < 4) ||
      (content !== undefined && content.length < 12) || (email !== undefined && !validEmail(email)) ||
      (body.like_count !== undefined && likeCount === undefined) || (body.created_at !== undefined && createdAt === undefined) ||
      (!kind && nickname === undefined && email === undefined && title === undefined && content === undefined && !status && !reward && reply === undefined && isFlagged === undefined && isPublic === undefined && likeCount === undefined && createdAt === undefined)) {
      return jsonError("Invalid update");
    }
    const { submission, previous } = await updateSubmission(id, {
      ...(kind ? { kind } : {}),
      ...(nickname !== undefined ? { nickname } : {}),
      ...(email !== undefined ? { email } : {}),
      ...(title !== undefined ? { title } : {}),
      ...(content !== undefined ? { body: content } : {}),
      ...(status ? { status } : {}),
      ...(reward ? { reward_status: reward } : {}),
      ...(reply !== undefined ? { developer_reply: reply || null } : {}),
      ...(isFlagged !== undefined ? { is_flagged: isFlagged } : {}),
      ...(isPublic !== undefined ? { is_public: status && status !== "accepted" ? false : isPublic } : {}),
      ...(likeCount !== undefined ? { like_count: likeCount } : {}),
      ...(createdAt !== undefined ? { created_at: createdAt } : {})
    });
    await safeRecordAccessEvent(request, {
      scope: "admin",
      eventType: "submission_updated",
      statusCode: 200,
      details: {
        submissionId: id,
        submissionTitle: submission.title,
        submissionKind: submission.kind,
        changes: changedSubmissionFields(previous, submission)
      }
    });
    return json({ submission });
  } catch {
    return jsonError("更新失败", 500);
  }
}

function cleanClientDetails(value) {
  const source = value && typeof value === "object" ? value : {};
  const text = (key, max) =>
    typeof source[key] === "string" ? source[key].slice(0, max) : null;
  const number = (key) =>
    typeof source[key] === "number" && Number.isFinite(source[key]) ? source[key] : null;
  const connection = source.connection && typeof source.connection === "object"
    ? source.connection
    : {};
  return {
    page_title: text("page_title", 200),
    page_url: text("page_url", 1200),
    timezone: text("timezone", 80),
    language: text("language", 50),
    languages: Array.isArray(source.languages)
      ? source.languages.filter((item) => typeof item === "string").slice(0, 12).map((item) => item.slice(0, 50))
      : [],
    platform: text("platform", 120),
    mobile: typeof source.mobile === "boolean" ? source.mobile : null,
    viewport: text("viewport", 30),
    screen: text("screen", 30),
    pixel_ratio: number("pixel_ratio"),
    color_depth: number("color_depth"),
    touch_points: number("touch_points"),
    hardware_concurrency: number("hardware_concurrency"),
    device_memory_gb: number("device_memory_gb"),
    cookies_enabled: typeof source.cookies_enabled === "boolean" ? source.cookies_enabled : null,
    do_not_track: text("do_not_track", 20),
    connection: {
      effective_type: typeof connection.effective_type === "string" ? connection.effective_type.slice(0, 30) : null,
      downlink_mbps: typeof connection.downlink_mbps === "number" && Number.isFinite(connection.downlink_mbps) ? connection.downlink_mbps : null,
      rtt_ms: typeof connection.rtt_ms === "number" && Number.isFinite(connection.rtt_ms) ? connection.rtt_ms : null,
      save_data: typeof connection.save_data === "boolean" ? connection.save_data : null
    },
    navigation_type: text("navigation_type", 30)
  };
}

async function handleAccess(request) {
  if (!isSameOrigin(request)) return new Response(null, { status: 403 });
  try {
    const body = await request.json();
    const path = typeof body.path === "string" ? body.path : "/";
    const details = cleanClientDetails(body.details);
    const pathname = new URL(path, request.url).pathname;
    const scope = pathname === "/admin" || pathname.startsWith("/admin/") ? "admin" : "public";
    await safeRecordAccessEvent(request, {
      scope,
      eventType: "page_view",
      path,
      method: "GET",
      statusCode: 200,
      referrer: typeof body.referrer === "string" ? body.referrer : undefined,
      details: {
        ...details,
        ...(scope === "admin" ? { authenticated: await isAdminRequest(request) } : {})
      }
    });
  } catch {
    // A failed audit write is intentionally invisible to the page visitor.
  }
  return new Response(null, { status: 204 });
}

async function handleAdminAccessLogs(request) {
  if (!(await isAdminRequest(request))) return jsonError("Unauthorized", 401);
  if (request.method === "GET") {
    try {
      return json(await listAccessLogs());
    } catch {
      return jsonError("无法读取访问日志", 500);
    }
  }
  if (request.method === "PATCH" && isSameOrigin(request)) {
    try {
      await acknowledgeAccessAlerts();
      await safeRecordAccessEvent(request, { scope: "admin", eventType: "security_alerts_acknowledged", statusCode: 200 });
      return json({ ok: true });
    } catch {
      return jsonError("操作失败", 500);
    }
  }
  await safeRecordAccessEvent(request, {
    scope: "admin",
    eventType: "unauthorized_alert_acknowledge",
    severity: isSameOrigin(request) ? "warning" : "critical",
    statusCode: 401
  });
  return jsonError("Unauthorized", 401);
}

function cleanTranslationEntries(value) {
  if (!Array.isArray(value)) return [];
  const usedKeys = new Set();
  return value
    .filter((item) => item && typeof item === "object")
    .map((item) => ({
      key: typeof item.key === "string" ? item.key.trim().slice(0, 120) : "",
      text: typeof item.text === "string" ? item.text.trim().slice(0, 2400) : ""
    }))
    .filter((item) => item.key && item.text && !usedKeys.has(item.key) && (usedKeys.add(item.key), true))
    .slice(0, 80);
}

function cleanTranslationTargets(value) {
  if (!Array.isArray(value)) return [];
  const usedLocales = new Set();
  return value
    .filter((item) => typeof item === "string")
    .map((item) => item.trim().toLowerCase().slice(0, 16))
    .filter((item) => /^[a-z]{2,3}(?:-[a-z0-9]{2,8})?$/.test(item) && !usedLocales.has(item) && (usedLocales.add(item), true))
    .slice(0, 8);
}

function parseTranslations(value, targets, entries) {
  const source = value && typeof value === "object" ? value : {};
  const translations = source.translations && typeof source.translations === "object"
    ? source.translations
    : source;
  const result = {};
  for (const locale of targets) {
    const language = translations[locale];
    if (!language || typeof language !== "object") throw new Error(`Translation response is missing ${locale}`);
    result[locale] = {};
    for (const entry of entries) {
      const translated = language[entry.key];
      if (typeof translated !== "string" || !translated.trim()) {
        throw new Error(`Translation response is missing ${locale}.${entry.key}`);
      }
      result[locale][entry.key] = translated.trim().slice(0, 2400);
    }
  }
  return result;
}

async function translateChineseContent(input) {
  const entries = cleanTranslationEntries(input.entries);
  const targetLocales = cleanTranslationTargets(input.targetLocales);
  if (!entries.length) throw new Error("请先填写中文内容");
  if (!targetLocales.length) throw new Error("请至少选择一种目标语言");
  if (!CONFIG.deepseekApiKey) throw new Error("DEEPSEEK_API_KEY is not configured");

  const response = await fetch(`${(CONFIG.deepseekBaseUrl || "https://api.deepseek.com").replace(/\/$/, "")}/chat/completions`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${CONFIG.deepseekApiKey}`,
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      model: CONFIG.deepseekModel || "deepseek-v4-flash",
      messages: [
        {
          role: "system",
          content: "You are a localization translator for a software product. Translate Chinese source strings into every requested target locale. Preserve line breaks, list structure, markdown, URLs, code, version numbers, product names, and placeholders exactly when appropriate. Do not add commentary. Return only JSON in this exact shape: {\"translations\": {\"<locale>\": {\"<key>\": \"translated text\"}}}. Every requested locale must contain every input key."
        },
        {
          role: "user",
          content: JSON.stringify({ source_locale: "zh-CN", target_locales: targetLocales, entries })
        }
      ],
      response_format: { type: "json_object" },
      temperature: 0.2,
      max_tokens: 8192,
      stream: false,
      thinking: { type: "disabled" }
    })
  });
  if (!response.ok) throw new Error(`翻译服务暂时不可用（HTTP ${response.status}）`);
  const data = await response.json();
  const content = data && data.choices && data.choices[0] && data.choices[0].message
    ? data.choices[0].message.content
    : "";
  if (!content) throw new Error("翻译服务未返回内容");
  try {
    return { translations: parseTranslations(JSON.parse(content), targetLocales, entries) };
  } catch (error) {
    if (error instanceof Error && error.message.startsWith("Translation response")) throw error;
    throw new Error("翻译结果格式异常，请重试");
  }
}

async function handleAdminTranslation(request) {
  if (!isSameOrigin(request) || !(await isAdminRequest(request))) return jsonError("Unauthorized", 401);
  if (request.method !== "POST") return jsonError("Method not allowed", 405);
  try {
    return json(await translateChineseContent(await request.json()));
  } catch (error) {
    const message = error instanceof Error ? error.message : "翻译失败";
    return jsonError(message, message.includes("not configured") ? 503 : 400);
  }
}

async function handleAdminPreviews(request) {
  if (!(await isAdminRequest(request))) return jsonError("Unauthorized", 401);
  if (request.method === "GET") {
    try {
      const records = await listReleasePreviews();
      return json({
        previews: records.filter((preview) => preview.status === "published"),
        drafts: records.filter((preview) => preview.status === "draft")
      });
    } catch {
      return jsonError("无法读取版本预告", 500);
    }
  }
  if (!isSameOrigin(request)) return jsonError("Unauthorized", 401);
  if (request.method === "POST") {
    try {
      const payload = previewPayload(await request.json());
      if (!payload.version || !payload.body_zh || !payload.body_en) {
        return jsonError("版本号、中英文更新内容均为必填项");
      }
      return json({ preview: await createReleasePreview(payload) }, 201);
    } catch {
      return jsonError("发布失败", 500);
    }
  }
  if (request.method === "PATCH") {
    try {
      const body = await request.json();
      const id = typeof body.id === "string" ? body.id : "";
      if (!id || (body.status !== "draft" && body.status !== "published")) {
        return jsonError("Invalid update");
      }
      const hasContent = ["version", "body_zh", "body_en", "body_zh_tw", "body_ja", "target_date"]
        .some((field) => Object.prototype.hasOwnProperty.call(body, field));
      const payload = hasContent ? previewPayload(body) : { status: body.status };
      if (hasContent && (!payload.version || !payload.body_zh || !payload.body_en)) {
        return jsonError("版本号、中英文更新内容均为必填项");
      }
      return json({
        preview: await updateReleasePreview(id, payload)
      });
    } catch {
      return jsonError("更新失败", 500);
    }
  }
  return jsonError("Method not allowed", 405);
}

async function handleAdminFeatures(request) {
  if (!(await isAdminRequest(request))) return jsonError("Unauthorized", 401);
  if (request.method === "GET") {
    try {
      return json({ content: await getFeatureContent() });
    } catch {
      return jsonError("无法读取新功能页内容", 500);
    }
  }
  if (request.method === "PUT" && isSameOrigin(request)) {
    try {
      const body = await request.json();
      return json({ content: await saveFeatureContent(body.content) });
    } catch (error) {
      const message = error instanceof Error && error.message.includes("At least one")
        ? "至少保留一条新功能内容"
        : error instanceof Error && error.message.includes("bilingual")
          ? "前台显示的条目必须补全中英文标题和描述"
          : "保存失败";
      return jsonError(message, 400);
    }
  }
  return jsonError("Unauthorized", 401);
}

async function handleRequest(request) {
  const path = new URL(request.url).pathname.replace(/\/+$/, "") || "/";
  if (path === "/api/access" && request.method === "POST") {
    return handleAccess(request);
  }
  if (path === "/api/incentives/public" && request.method === "GET") {
    return handlePublic(request);
  }
  if (path === "/api/features" && request.method === "GET") {
    return handleFeatures();
  }
  if (path === "/api/incentives/submissions" && request.method === "POST") {
    return handleSubmission(request);
  }
  if (path === "/api/incentives/likes" && request.method === "POST") {
    return handleLike(request);
  }
  if (path === "/api/incentives/admin/login" && request.method === "POST") {
    return handleLogin(request);
  }
  if (path === "/api/incentives/admin/logout" && request.method === "POST") {
    return handleLogout(request);
  }
  if (path === "/api/incentives/admin/submissions") {
    return handleAdminSubmissions(request);
  }
  if (path === "/api/incentives/admin/previews") {
    return handleAdminPreviews(request);
  }
  if (path === "/api/incentives/admin/features") {
    return handleAdminFeatures(request);
  }
  if (path === "/api/incentives/admin/translate") {
    return handleAdminTranslation(request);
  }
  if (path === "/api/incentives/admin/access-logs") {
    return handleAdminAccessLogs(request);
  }
  return jsonError("Not found", 404);
}

export default {
  fetch(request) {
    return handleRequest(request);
  }
};
