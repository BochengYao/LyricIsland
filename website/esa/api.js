// This source is converted into esa-dist/entry.js during deployment.
// Build-time placeholders are injected only into the server-side function artifact.
const CONFIG = Object.freeze({
  supabaseUrl: "__ESA_SUPABASE_URL__",
  supabaseKey: "__ESA_SUPABASE_SERVICE_ROLE_KEY__",
  storageBucket: "__ESA_SUPABASE_STORAGE_BUCKET__",
  adminPassword: "__ESA_ADMIN_PASSWORD__",
  adminSessionSecret: "__ESA_ADMIN_SESSION_SECRET__"
});

const ADMIN_COOKIE = "lyric_island_admin";
const VOTER_COOKIE = "lyric_island_voter";
const SESSION_SECONDS = 60 * 60 * 24 * 7;
const VOTER_SECONDS = 60 * 60 * 24 * 365;
const MAX_FILES = 3;
const MAX_FILE_SIZE = 15 * 1024 * 1024;
const MAX_TOTAL_SIZE = 30 * 1024 * 1024;
const REVIEW_META_PREFIX = "[[lyric-island-review:v1]]";
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
    return { developer_reply: value || null, is_flagged: false, is_public: false };
  }
  try {
    const parsed = JSON.parse(value.slice(REVIEW_META_PREFIX.length));
    return {
      developer_reply: typeof parsed.reply === "string" && parsed.reply ? parsed.reply : null,
      is_flagged: parsed.flagged === true,
      is_public: parsed.public === true
    };
  } catch {
    return { developer_reply: null, is_flagged: false, is_public: false };
  }
}

function encodeReviewMeta(meta) {
  return `${REVIEW_META_PREFIX}${JSON.stringify({
    reply: meta.developer_reply || "",
    flagged: meta.is_flagged === true,
    public: meta.is_public === true
  })}`;
}

function toSubmission(row) {
  const { reviewer_note, ...submission } = row;
  return { ...submission, ...decodeReviewMeta(reviewer_note) };
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
  return response.json();
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

function isSameOrigin(request) {
  const origin = request.headers.get("origin");
  return !origin || origin === new URL(request.url).origin;
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
  const rows = await supabase("/rest/v1/incentive_submissions", {
    method: "POST",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify(input)
  });
  return rows[0];
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
    "/rest/v1/release_previews?select=*&status=eq.published&order=published_at.desc&limit=6"
  );
  const [rows, likedRows, previews] = await Promise.all([
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
  return { suggestions, previews };
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
  const liked = existing.length === 0;
  if (liked) {
    await supabase("/rest/v1/incentive_likes", {
      method: "POST",
      headers: supabaseHeaders("return=representation"),
      body: JSON.stringify({ submission_id: submissionId, voter_token_hash: voterTokenHash })
    });
  } else {
    await supabase(
      `/rest/v1/incentive_likes?submission_id=eq.${encodeURIComponent(submissionId)}&voter_token_hash=eq.${encodeURIComponent(voterTokenHash)}`,
      { method: "DELETE", headers: supabaseHeaders("return=representation") }
    );
  }
  const likeCount = Math.max(0, submission.like_count + (liked ? 1 : -1));
  await supabase(`/rest/v1/incentive_submissions?id=eq.${encodeURIComponent(submissionId)}`, {
    method: "PATCH",
    headers: supabaseHeaders("return=representation"),
    body: JSON.stringify({ like_count: likeCount })
  });
  return { liked, like_count: likeCount };
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
  const currentMeta = decodeReviewMeta(current.reviewer_note);
  const { developer_reply, is_flagged, is_public, ...storedChanges } = changes;
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
          is_public: is_public !== undefined ? is_public : currentMeta.is_public
        }),
        updated_at: new Date().toISOString()
      })
    }
  );
  return toSubmission(rows[0]);
}

async function listReleasePreviews() {
  return supabase(
    "/rest/v1/release_previews?select=*&order=created_at.desc&limit=50"
  );
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
  return rows[0];
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
  return rows[0];
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
  return {
    version,
    title_zh: version,
    title_en: version,
    body_zh: typeof body.body_zh === "string" ? body.body_zh.trim().slice(0, 2400) : "",
    body_en: typeof body.body_en === "string" ? body.body_en.trim().slice(0, 2400) : "",
    highlights_zh: lines(body.highlights_zh),
    highlights_en: lines(body.highlights_en),
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
      attachments
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
  if (!isSameOrigin(request)) return jsonError("Invalid origin", 403);
  try {
    const body = await request.json();
    const password = typeof body.password === "string" ? body.password : "";
    if (!(await verifyAdminPassword(password))) {
      return jsonError("密码不正确", 401);
    }
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

async function handleAdminSubmissions(request) {
  if (!(await isAdminRequest(request))) return jsonError("Unauthorized", 401);
  if (request.method === "GET") {
    try {
      return json({ submissions: await listSubmissions() });
    } catch {
      return jsonError("无法读取提交记录", 500);
    }
  }
  if (request.method !== "PATCH") return jsonError("Method not allowed", 405);
  if (!isSameOrigin(request)) return jsonError("Unauthorized", 401);
  try {
    const body = await request.json();
    const statuses = ["pending", "reviewing", "accepted", "declined"];
    const rewards = ["not_eligible", "pending", "issued"];
    const id = typeof body.id === "string" ? body.id : "";
    const status = statuses.includes(body.status) ? body.status : undefined;
    const reward = rewards.includes(body.reward_status) ? body.reward_status : undefined;
    const reply =
      typeof body.developer_reply === "string"
        ? body.developer_reply.trim().slice(0, 2000)
        : undefined;
    const isFlagged = typeof body.is_flagged === "boolean" ? body.is_flagged : undefined;
    const isPublic = typeof body.is_public === "boolean" ? body.is_public : undefined;
    if (!id || (!status && !reward && reply === undefined && isFlagged === undefined && isPublic === undefined)) {
      return jsonError("Invalid update");
    }
    const submission = await updateSubmission(id, {
      ...(status ? { status } : {}),
      ...(reward ? { reward_status: reward } : {}),
      ...(reply !== undefined ? { developer_reply: reply || null } : {}),
      ...(isFlagged !== undefined ? { is_flagged: isFlagged } : {}),
      ...(isPublic !== undefined ? { is_public: status && status !== "accepted" ? false : isPublic } : {})
    });
    return json({ submission });
  } catch {
    return jsonError("更新失败", 500);
  }
}

async function handleAdminPreviews(request) {
  if (!(await isAdminRequest(request))) return jsonError("Unauthorized", 401);
  if (request.method === "GET") {
    try {
      return json({ previews: await listReleasePreviews() });
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
      return json({
        preview: await updateReleasePreview(id, { status: body.status })
      });
    } catch {
      return jsonError("更新失败", 500);
    }
  }
  return jsonError("Method not allowed", 405);
}

async function handleRequest(request) {
  const path = new URL(request.url).pathname.replace(/\/+$/, "") || "/";
  if (path === "/api/incentives/public" && request.method === "GET") {
    return handlePublic(request);
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
  return jsonError("Not found", 404);
}

export default {
  fetch(request) {
    return handleRequest(request);
  }
};
