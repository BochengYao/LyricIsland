import assert from "node:assert/strict";
import { File as NodeFile } from "node:buffer";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { resolve } from "node:path";
import { join } from "node:path";
import { tmpdir } from "node:os";

const root = resolve(import.meta.dirname, "..");
const buildDirectory = await mkdtemp(join(tmpdir(), "lyric-island-esa-"));
const testEntry = resolve(buildDirectory, "entry.mjs");
const values = {
  "__ESA_SUPABASE_URL__": "https://example.supabase.co",
  "__ESA_SUPABASE_SERVICE_ROLE_KEY__": "sb_secret_test_only",
  "__ESA_SUPABASE_STORAGE_BUCKET__": "lyric-island-submissions",
  "__ESA_ADMIN_PASSWORD__": "correct horse battery staple",
  "__ESA_ADMIN_SESSION_SECRET__": "test-session-secret-with-at-least-32-characters",
  "__ESA_DEEPSEEK_API_KEY__": "deepseek-test-key",
  "__ESA_DEEPSEEK_BASE_URL__": "https://api.deepseek.com",
  "__ESA_DEEPSEEK_MODEL__": "deepseek-v4-flash",
  "__ESA_FEATURE_CONTENT_JSON__": await readFile(
    resolve(root, "data", "feature-content-default.json"),
    "utf8"
  ),
  "__ESA_RELEASE_PREVIEW_JSON__": await readFile(
    resolve(root, "data", "release-preview-default.json"),
    "utf8"
  )
};

let source = await readFile(resolve(root, "esa", "api.js"), "utf8");
for (const [marker, value] of Object.entries(values)) {
  source = source.replaceAll(JSON.stringify(marker), JSON.stringify(value));
}
assert.ok(!source.includes("__ESA_"), "all deployment placeholders must be replaced");
assert.ok(!source.includes("process.env"), "the ESA runtime entry must not depend on Node.js globals");

await writeFile(testEntry, source, "utf8");
if (!globalThis.File) globalThis.File = NodeFile;

const originalFetch = globalThis.fetch;
const calls = [];
let reviewerNote = '[[lyric-island-review:v1]]{"reply":"Planned for the next version.","flagged":false,"public":true}';
let submissionStatus = "accepted";
let rewardStatus = "not_eligible";
let hasLike = true;
let likeCount = 1;
let auditRows = [];
let insertedSubmissions = [];
let featureRow = null;
let releasePreviewRows = [];
let promoCodeRows = [];
let promoCodeLogRows = [];
let promoCodeRpcStats = null;
let promoCodeAllocateResult = null;
let promoCodeBulkImportResult = null;

function publicPreviewRow(id, version, publishedAt) {
  return {
    id,
    version,
    title_zh: version,
    title_en: version,
    title_zh_tw: version,
    title_ja: version,
    body_zh: `${version} 中文更新。`,
    body_en: `${version} English updates.`,
    body_zh_tw: `${version} 繁中更新。`,
    body_ja: `${version} 日本語の更新。`,
    highlights_zh: [],
    highlights_en: [],
    highlights_zh_tw: [],
    highlights_ja: [],
    target_date: null,
    status: "published",
    created_at: publishedAt,
    updated_at: publishedAt,
    published_at: publishedAt
  };
}

function storedSubmission() {
  return {
    id: "11111111-1111-4111-8111-111111111111",
    kind: "feature",
    nickname: "Tester",
    email: "tester@example.com",
    title: "A useful suggestion",
    body: "This is a sufficiently detailed public suggestion.",
    attachments: [
      {
        path: "11111111-1111-4111-8111-111111111111/image.png",
        name: "image.png",
        type: "image/png",
        size: 1
      }
    ],
    like_count: likeCount,
    status: submissionStatus,
    reward_status: rewardStatus,
    reviewer_note: reviewerNote,
    created_at: "2026-07-18T00:00:00.000Z",
    updated_at: "2026-07-18T00:00:00.000Z"
  };
}

function response(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" }
  });
}

globalThis.fetch = async (input, init = {}) => {
  const url = String(input);
  calls.push({ url, init });
  if (url === "https://api.deepseek.com/chat/completions") {
    assert.equal(init.headers.Authorization, "Bearer deepseek-test-key");
    const requestBody = JSON.parse(init.body);
    assert.equal(requestBody.model, "deepseek-v4-flash");
    assert.equal(requestBody.response_format.type, "json_object");
    const translationInput = JSON.parse(requestBody.messages.at(-1).content);
    const translations = Object.fromEntries(translationInput.target_locales.map((locale) => [
      locale,
      Object.fromEntries(translationInput.entries.map((entry) => [entry.key, `${locale}:${entry.text}`]))
    ]));
    return response({ choices: [{ message: { content: JSON.stringify({ translations }) } }] });
  }
  if (url.endsWith("/rest/v1/incentive_likes") && init.method === "POST") {
    hasLike = true;
    return response([JSON.parse(init.body)], 201);
  }
  if (url.includes("/rest/v1/incentive_likes?")) {
    return response(hasLike ? [{ submission_id: "11111111-1111-4111-8111-111111111111" }] : []);
  }
  if (url.includes("incentive_submissions?select=id,like_count,reviewer_note,status")) {
    return response([storedSubmission()]);
  }
  if (url.includes("incentive_submissions?select=id,kind,nickname,title,body,created_at,like_count,attachments,reviewer_note,status")) {
    return response([
      {
        id: "11111111-1111-4111-8111-111111111111",
        kind: "feature",
        nickname: "Tester",
        title: "A useful suggestion",
        body: "This is a sufficiently detailed public suggestion.",
        created_at: "2026-07-18T00:00:00.000Z",
        like_count: likeCount,
        reviewer_note: reviewerNote,
        status: submissionStatus,
        attachments: [
          {
            path: "11111111-1111-4111-8111-111111111111/image.png",
            name: "image.png",
            type: "image/png",
            size: 1
          }
        ]
      }
    ]);
  }
  if (url.includes("incentive_submissions?select=id,kind,nickname,title,body,reviewer_note,created_at")) {
    return response([storedSubmission(), ...insertedSubmissions]);
  }
  if (url.includes("release_previews?select=*&status=eq.published")) {
    const query = new URL(url).searchParams;
    const cursorFilter = query.get("or")?.match(/published_at\.lt\.([^,]+),and\(published_at\.eq\.([^,]+),id\.lt\.([^\)]+)\)\)/);
    let rows = releasePreviewRows
      .filter((row) => row.status === "published")
      .sort((left, right) => String(right.published_at).localeCompare(String(left.published_at)) || String(right.id).localeCompare(String(left.id)));
    if (cursorFilter) {
      const [, beforePublishedAt, equalPublishedAt, beforeId] = cursorFilter;
      rows = rows.filter((row) => String(row.published_at) < beforePublishedAt || (String(row.published_at) === equalPublishedAt && String(row.id) < beforeId));
    }
    return response(rows.slice(0, Number(query.get("limit") ?? rows.length)));
  }
  if (url.includes("release_previews?select=*&version=not.in.")) {
    return response(releasePreviewRows);
  }
  if (url.includes("release_previews?select=id,title_zh,title_en,body_zh,body_en,highlights_zh,created_at,published_at")) {
    return response(auditRows);
  }
  if (url.includes(`release_previews?select=*&version=eq.${encodeURIComponent("__FEATURE_CONTENT_V1__")}`)) {
    return response(featureRow ? [featureRow] : []);
  }
  if (url.includes("/storage/v1/object/sign/lyric-island-submissions")) {
    const paths = JSON.parse(init.body).paths;
    return response(
      paths.map((path) => ({
        path,
        signedURL: `/object/sign/lyric-island-submissions/${path}?token=test`
      }))
    );
  }
  if (url.includes("incentive_submissions?select=*&order=created_at.desc")) {
    return response([storedSubmission()]);
  }
  if (url.includes("incentive_submissions?select=*&id=eq.")) {
    return response([storedSubmission()]);
  }
  if (url.includes("/rest/v1/incentive_submissions?id=eq.") && init.method === "PATCH") {
    const body = JSON.parse(init.body);
    if ("reviewer_note" in body) reviewerNote = body.reviewer_note;
    submissionStatus = body.status ?? submissionStatus;
    rewardStatus = body.reward_status ?? rewardStatus;
    likeCount = body.like_count ?? likeCount;
    return response([{ ...storedSubmission(), ...body }]);
  }
  if (url.includes("/rest/v1/incentive_submissions?id=eq.") && init.method === "DELETE") {
    return response([storedSubmission()]);
  }
  if (url.endsWith("/storage/v1/object/lyric-island-submissions/placeholder")) {
    return response({});
  }
  if (url.includes("/storage/v1/object/lyric-island-submissions/")) {
    return response({ Key: "uploaded" });
  }
  if (url.endsWith("/rest/v1/incentive_submissions") && init.method === "POST") {
    const body = JSON.parse(init.body);
    const row = {
      ...body,
      status: "pending",
      reward_status: "pending",
      like_count: 0,
      created_at: "2026-07-22T00:02:00.000Z",
      updated_at: "2026-07-22T00:02:00.000Z"
    };
    insertedSubmissions.unshift(row);
    return response([row], 201);
  }
  if (url.endsWith("/rest/v1/release_previews") && init.method === "POST") {
    const body = JSON.parse(init.body);
    const row = {
      id: "22222222-2222-4222-8222-222222222222",
      ...body,
      created_at: "2026-07-18T00:00:00.000Z",
      updated_at: "2026-07-18T00:00:00.000Z"
    };
    if (body.version === "__AUDIT_LOG_V1__") {
      row.id = `audit-${auditRows.length + 1}`;
      row.created_at = new Date(Date.UTC(2026, 6, 22, 0, 0, auditRows.length)).toISOString();
      auditRows.unshift(row);
    } else if (body.version === "__FEATURE_CONTENT_V1__") {
      featureRow = row;
    } else {
      row.id = `preview-${releasePreviewRows.length + 1}`;
      releasePreviewRows.unshift(row);
    }
    return response([row], 201);
  }
  if (url.includes(`release_previews?version=eq.${encodeURIComponent("__AUDIT_LOG_V1__")}`) && init.method === "PATCH") {
    const body = JSON.parse(init.body);
    const severity = new URL(url).searchParams.get("title_en");
    auditRows = auditRows.map((item) => item.title_en === severity?.replace("eq.", "")
      ? { ...item, ...body }
      : item);
    return response([]);
  }
  if (url.includes("/rest/v1/release_previews?id=eq.") && init.method === "PATCH") {
    const body = JSON.parse(init.body);
    const id = decodeURIComponent(url.split("id=eq.")[1]);
    const previewIndex = releasePreviewRows.findIndex((row) => row.id === id);
    if (previewIndex >= 0) {
      releasePreviewRows[previewIndex] = { ...releasePreviewRows[previewIndex], ...body };
      return response([releasePreviewRows[previewIndex]]);
    }
    featureRow = { ...featureRow, ...body };
    return response([featureRow]);
  }
  // ── Promo Code Supabase mock handlers ──

  // List promo codes (supabaseRaw with Prefer: count=exact)
  if (url.includes("/rest/v1/promo_codes?select=*&order=created_at.desc") && !init.method) {
    const parsed = new URL(url);
    const limit = Number(parsed.searchParams.get("limit") || 20);
    const offset = Number(parsed.searchParams.get("offset") || 0);
    const statusFilter = parsed.searchParams.get("distribution_status");
    const searchOr = parsed.searchParams.get("or");
    let rows = [...promoCodeRows];
    if (statusFilter) {
      const statusValue = statusFilter.replace("eq.", "");
      rows = rows.filter((r) => r.distribution_status === statusValue);
    }
    if (searchOr) {
      const match = searchOr.match(/code\.ilike\.\*([^*]+)\*/);
      if (match) {
        const term = match[1].toLowerCase();
        rows = rows.filter((r) =>
          [r.code, r.microsoft_code_id, r.assigned_to_name, r.assigned_to_email, r.campaign, r.raw_order_id, r.note]
            .some((v) => v && String(v).toLowerCase().includes(term))
        );
      }
    }
    const total = rows.length;
    const sliced = rows.slice(offset, offset + limit);
    return new Response(JSON.stringify(sliced), {
      status: 200,
      headers: { "Content-Type": "application/json", "Content-Range": `${offset}-${offset + sliced.length - 1}/${total}` }
    });
  }

  // RPC: promo_code_dashboard_stats
  if (url.includes("/rest/v1/rpc/promo_code_dashboard_stats")) {
    const stats = promoCodeRpcStats || {
      total_codes: promoCodeRows.length,
      available: promoCodeRows.filter((r) => r.distribution_status === "available").length,
      allocated: promoCodeRows.filter((r) => r.distribution_status === "allocated").length,
      redeemed: promoCodeRows.filter((r) => r.distribution_status === "redeemed").length
    };
    return response([stats]);
  }

  // RPC: bulk_import_promo_codes
  if (url.includes("/rest/v1/rpc/bulk_import_promo_codes")) {
    const result = promoCodeBulkImportResult || { created: 0, updated: 0, unchanged: 0 };
    return response(result);
  }

  // RPC: allocate_promo_code
  if (url.includes("/rest/v1/rpc/allocate_promo_code")) {
    if (promoCodeAllocateResult) return response([promoCodeAllocateResult]);
    return response([]);
  }

  // GET promo code logs for a specific code
  if (url.includes("/rest/v1/promo_code_logs?promo_code_id=eq.") && !init.method) {
    const id = decodeURIComponent(url.split("promo_code_id=eq.")[1].split("&")[0]);
    const logs = promoCodeLogRows.filter((r) => r.promo_code_id === id);
    return response(logs);
  }

  // POST promo code log
  if (url.endsWith("/rest/v1/promo_code_logs") && init.method === "POST") {
    const body = JSON.parse(init.body);
    promoCodeLogRows.push(body);
    return response([body], 201);
  }

  // GET promo codes by microsoft_code_id IN (...)
  if (url.includes("/rest/v1/promo_codes?select=*&microsoft_code_id=in.")) {
    return response([]);
  }

  // GET single promo code for delete check (select=id,distribution_status)
  if (url.includes("/rest/v1/promo_codes?select=id,distribution_status&id=eq.")) {
    const id = decodeURIComponent(url.split("id=eq.")[1].split("&")[0]);
    const found = promoCodeRows.filter((r) => r.id === id);
    return response(found);
  }

  // GET single promo code detail
  if (url.includes("/rest/v1/promo_codes?select=*&id=eq.") && !init.method) {
    const id = decodeURIComponent(url.split("id=eq.")[1].split("&")[0]);
    const found = promoCodeRows.filter((r) => r.id === id);
    return response(found);
  }

  // PATCH promo code
  if (url.includes("/rest/v1/promo_codes?id=eq.") && init.method === "PATCH") {
    const id = decodeURIComponent(url.split("id=eq.")[1]);
    const body = JSON.parse(init.body);
    const idx = promoCodeRows.findIndex((r) => r.id === id);
    if (idx >= 0) {
      promoCodeRows[idx] = { ...promoCodeRows[idx], ...body };
      return response([promoCodeRows[idx]]);
    }
    return response([]);
  }

  // DELETE promo code
  if (url.includes("/rest/v1/promo_codes?id=eq.") && init.method === "DELETE") {
    const id = decodeURIComponent(url.split("id=eq.")[1]);
    const idx = promoCodeRows.findIndex((r) => r.id === id);
    if (idx >= 0) promoCodeRows.splice(idx, 1);
    return new Response(null, { status: 204 });
  }

  throw new Error(`Unexpected fetch in ESA API test: ${url}`);
};

try {
  const api = (await import(`${pathToFileURL(testEntry).href}?v=${Date.now()}`)).default;

  calls.length = 0;
  const publicResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/public", {
      headers: { cookie: "lyric_island_voter=test-voter" }
    })
  );
  assert.equal(publicResponse.status, 200);
  const publicData = await publicResponse.json();
  assert.equal(publicData.configured, true);
  assert.equal(publicData.suggestions[0].liked, true);
  assert.equal(publicData.suggestions[0].developer_reply, "Planned for the next version.");
  assert.match(publicData.suggestions[0].attachment.url, /token=test$/);
  assert.equal(publicData.previews[0].version, "v2.1");
  assert.equal(publicData.previews[0].major_version, "V2");
  assert.equal(publicData.next_preview_cursor, null);
  assert.equal(publicData.previews[0].target_date, null);
  assert.deepEqual(publicData.previews[0].highlights_zh, [
    "自定义LyricHover形状",
    "自定义字体、颜色",
    "自定义各模块颜色"
  ]);
  assert.equal(publicData.previews[0].body_zh_tw, publicData.previews[0].body_zh);
  assert.equal(publicData.previews[0].body_ja, publicData.previews[0].body_en);
  assert.deepEqual(publicData.previews[0].highlights_zh_tw, publicData.previews[0].highlights_zh);
  assert.deepEqual(publicData.previews[0].highlights_ja, publicData.previews[0].highlights_en);
  assert.equal(calls.length, 4, "public API must stay within ESA's four-subrequest limit");
  assert.ok(
    calls.every((call) => call.init.headers.apikey === values.__ESA_SUPABASE_SERVICE_ROLE_KEY__),
    "every Supabase request must send the secret in the apikey header"
  );
  assert.ok(
    calls.every((call) => !("Authorization" in call.init.headers)),
    "opaque sb_secret_ keys must not be sent as bearer JWTs"
  );

  releasePreviewRows = [
    publicPreviewRow("preview-v3", "v3.0", "2026-08-03T00:00:00.000Z"),
    publicPreviewRow("preview-v2-5", "v2.5", "2026-08-02T00:00:00.000Z"),
    publicPreviewRow("preview-v2-1", "v2.1", "2026-08-01T00:00:00.000Z")
  ];
  const firstPreviewPageResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/public?preview_limit=2")
  );
  assert.equal(firstPreviewPageResponse.status, 200);
  const firstPreviewPage = await firstPreviewPageResponse.json();
  assert.deepEqual(firstPreviewPage.previews.map((preview) => preview.version), ["v3.0", "v2.5"]);
  assert.equal(firstPreviewPage.previews[0].major_version, "V3");
  assert.ok(firstPreviewPage.next_preview_cursor, "a full page must return a cursor");
  const secondPreviewPageResponse = await api.fetch(
    new Request(`https://lyric-island.top/api/incentives/public?preview_limit=2&preview_cursor=${encodeURIComponent(firstPreviewPage.next_preview_cursor)}`)
  );
  assert.equal(secondPreviewPageResponse.status, 200);
  const secondPreviewPage = await secondPreviewPageResponse.json();
  assert.deepEqual(secondPreviewPage.previews.map((preview) => preview.version), ["v2.1"]);
  assert.equal(secondPreviewPage.next_preview_cursor, null);
  const invalidCursorResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/public?preview_cursor=bad")
  );
  assert.equal(invalidCursorResponse.status, 400);
  releasePreviewRows = [];

  calls.length = 0;
  const duplicateLikeResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/likes", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: "lyric_island_voter=test-voter"
      },
      body: JSON.stringify({ submissionId: "11111111-1111-4111-8111-111111111111" })
    })
  );
  assert.equal(duplicateLikeResponse.status, 200);
  const duplicateLikeData = await duplicateLikeResponse.json();
  assert.equal(duplicateLikeData.liked, true);
  assert.equal(duplicateLikeData.like_count, 1);
  assert.equal(duplicateLikeData.already_liked, true);
  assert.equal(calls.length, 2, "a repeated device like must only verify the card and existing vote");
  assert.ok(
    calls.every((call) => !["POST", "PATCH", "DELETE"].includes(call.init.method)),
    "a repeated device like must not mutate either the vote or the count"
  );

  hasLike = false;
  likeCount = 1;
  calls.length = 0;
  const firstLikeResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/likes", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ submissionId: "11111111-1111-4111-8111-111111111111" })
    })
  );
  assert.equal(firstLikeResponse.status, 200);
  const firstLikeData = await firstLikeResponse.json();
  assert.equal(firstLikeData.liked, true);
  assert.equal(firstLikeData.like_count, 2);
  assert.equal(firstLikeData.already_liked, false);
  const voterCookie = firstLikeResponse.headers.get("set-cookie");
  assert.match(voterCookie, /lyric_island_voter=.*HttpOnly/);
  assert.equal(calls.length, 4, "a first device like must create one vote and update the aggregate count");

  calls.length = 0;
  const repeatedLikeResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/likes", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: voterCookie.split(";")[0]
      },
      body: JSON.stringify({ submissionId: "11111111-1111-4111-8111-111111111111" })
    })
  );
  const repeatedLikeData = await repeatedLikeResponse.json();
  assert.equal(repeatedLikeData.liked, true);
  assert.equal(repeatedLikeData.like_count, 2);
  assert.equal(repeatedLikeData.already_liked, true);
  assert.equal(calls.length, 2, "the same device cannot increment the same card twice");

  calls.length = 0;
  const publicFeaturesResponse = await api.fetch(
    new Request("https://lyric-island.top/api/features")
  );
  assert.equal(publicFeaturesResponse.status, 200);
  const publicFeaturesData = await publicFeaturesResponse.json();
  assert.equal(publicFeaturesData.content.summary.label_zh, "本次重点");
  assert.equal(publicFeaturesData.content.sections.length, 6);
  assert.equal(publicFeaturesData.content.sections[0].release_version, "早期更新");
  assert.equal(publicFeaturesData.content.sections[0].major_version, "OTHER");
  assert.equal(calls.length, 2, "first feature read must import the bundled content in two requests");

  const loginResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/login", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        "client-ip-geo-location": "CN"
      },
      body: JSON.stringify({ password: values.__ESA_ADMIN_PASSWORD__ })
    })
  );
  assert.equal(loginResponse.status, 200);
  const adminCookie = loginResponse.headers.get("set-cookie");
  assert.match(adminCookie, /HttpOnly; Secure; SameSite=Strict/);

  calls.length = 0;
  const translationResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/translate", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({
        targetLocales: ["en", "zh-tw", "ja"],
        entries: [{ key: "preview.body", text: "支持自定义歌词岛形状" }]
      })
    })
  );
  assert.equal(translationResponse.status, 200);
  const translationData = await translationResponse.json();
  assert.equal(translationData.translations.en["preview.body"], "en:支持自定义歌词岛形状");
  assert.equal(translationData.translations["zh-tw"]["preview.body"], "zh-tw:支持自定义歌词岛形状");
  assert.equal(translationData.translations.ja["preview.body"], "ja:支持自定义歌词岛形状");
  assert.equal(calls.length, 1, "translation must make exactly one server-side DeepSeek request");

  const invalidFeatures = structuredClone(publicFeaturesData.content);
  invalidFeatures.sections[0].release_version = "";
  const invalidFeatureSaveResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/features", {
      method: "PUT",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({ content: invalidFeatures })
    })
  );
  assert.equal(invalidFeatureSaveResponse.status, 400, "new feature entries require a release version");

  const managedFeatures = structuredClone(publicFeaturesData.content);
  managedFeatures.sections[0].release_version = "v2.1.8";
  managedFeatures.sections[1].release_version = "v3.0.0";
  managedFeatures.sections[0].title_zh = "后台修改后的标题";
  managedFeatures.sections[0].title_zh_tw = "後台修改後的標題";
  managedFeatures.sections[0].title_ja = "管理画面で変更した見出し";
  managedFeatures.sections.reverse();
  const featureSaveResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/features", {
      method: "PUT",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({ content: managedFeatures })
    })
  );
  assert.equal(featureSaveResponse.status, 200);
  const featureSaveData = await featureSaveResponse.json();
  assert.equal(featureSaveData.content.sections[5].title_zh, "后台修改后的标题");
  assert.equal(featureSaveData.content.sections[5].title_zh_tw, "後台修改後的標題");
  assert.equal(featureSaveData.content.sections[5].title_ja, "管理画面で変更した見出し");
  assert.equal(featureSaveData.content.sections[5].release_version, "v2.1.8");
  assert.equal(featureSaveData.content.sections[5].major_version, "V2");
  assert.equal(featureSaveData.content.sections[4].release_version, "v3.0.0");
  assert.equal(featureSaveData.content.sections[4].major_version, "V3");
  assert.equal(featureSaveData.content.sections[0].id, "feature-06");

  const proxiedLoginResponse = await api.fetch(
    new Request("https://internal-worker.local/api/incentives/admin/login", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        "x-forwarded-host": "lyric-island.top",
        "x-forwarded-proto": "https"
      },
      body: JSON.stringify({ password: values.__ESA_ADMIN_PASSWORD__ })
    })
  );
  assert.equal(proxiedLoginResponse.status, 200, "proxied same-origin logins must use the public host");

  calls.length = 0;
  const adminResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/submissions", {
      headers: { cookie: adminCookie.split(";")[0] }
    })
  );
  assert.equal(adminResponse.status, 200);
  const adminData = await adminResponse.json();
  assert.match(adminData.submissions[0].attachments[0].signedUrl, /token=test$/);
  assert.equal(calls.length, 2, "admin queue must batch attachment signing");

  calls.length = 0;
  const saveResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/submissions", {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({
        id: "11111111-1111-4111-8111-111111111111",
        status: "accepted",
        reward_status: "pending",
        developer_reply: "Confirmed for the next release.",
        is_flagged: true,
        is_public: true,
        like_count: 37,
        created_at: "2026-07-20T04:30:00.000Z"
      })
    })
  );
  assert.equal(saveResponse.status, 200);
  const saveData = await saveResponse.json();
  assert.equal(saveData.submission.status, "accepted");
  assert.equal(saveData.submission.reward_status, "pending");
  assert.equal(saveData.submission.developer_reply, "Confirmed for the next release.");
  assert.equal(saveData.submission.is_flagged, true);
  assert.equal(saveData.submission.is_public, true);
  assert.equal(saveData.submission.like_count, 37);
  assert.equal(saveData.submission.created_at, "2026-07-20T04:30:00.000Z");
  assert.equal(calls.length, 3, "saving a review must read, update and append an audit record");

  const pageAccessResponse = await api.fetch(
    new Request("https://lyric-island.top/api/access", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        "ali-cdn-real-ip": "203.0.113.10",
        "x-forwarded-for": "203.0.113.10, 10.0.0.1",
        "ali-ip-country": "CN",
        "ali-ip-city": "Hangzhou",
        "accept-language": "zh-CN,zh;q=0.9",
        "x-request-id": "request-page-view-001",
        "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Edg/140.0"
      },
      body: JSON.stringify({
        path: "/incentives?campaign=summer&token=secret-value",
        referrer: "https://example.com/",
        details: {
          page_title: "用户激励计划",
          timezone: "Asia/Shanghai",
          viewport: "1440×900",
          screen: "1920×1080"
        }
      })
    })
  );
  assert.equal(pageAccessResponse.status, 204);
  assert.equal(auditRows[0].title_zh, "page_view");
  assert.equal(auditRows[0].highlights_zh.scope, "public");
  assert.equal(auditRows[0].highlights_zh.visitor_hash.length, 64);
  assert.equal(auditRows[0].highlights_zh.ip_address, "203.0.113.10");
  assert.equal(auditRows[0].highlights_zh.ip_source, "ali-cdn-real-ip");
  assert.equal(auditRows[0].highlights_zh.country, "CN");
  assert.equal(auditRows[0].highlights_zh.city, "Hangzhou");
  assert.equal(auditRows[0].body_zh, "/incentives?campaign=summer&token=%5Bredacted%5D");
  assert.equal(auditRows[0].body_en, "GET");
  assert.equal(auditRows[0].highlights_zh.details.timezone, "Asia/Shanghai");

  const failedLoginResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/login", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        "x-forwarded-for": "198.51.100.20"
      },
      body: JSON.stringify({ password: "wrong password" })
    })
  );
  assert.equal(failedLoginResponse.status, 401);
  assert.equal(auditRows[0].title_zh, "login_failed");
  assert.equal(auditRows[0].title_en, "warning");

  const accessLogResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/access-logs", {
      headers: { cookie: adminCookie.split(";")[0] }
    })
  );
  assert.equal(accessLogResponse.status, 200);
  const accessLogData = await accessLogResponse.json();
  assert.ok(accessLogData.logs.length >= 2);
  assert.equal(accessLogData.unreadAlerts, 1);
  const successfulLoginLog = accessLogData.logs.find(
    (item) => item.event_type === "login_succeeded" && item.country === "CN"
  );
  assert.ok(successfulLoginLog?.created_at, "successful admin logins must retain their login time");
  assert.equal(successfulLoginLog.country, "CN");
  const pageViewLog = accessLogData.logs.find((item) => item.event_type === "page_view");
  assert.equal(pageViewLog.ip_address, "203.0.113.10");
  assert.equal(pageViewLog.city, "Hangzhou");
  assert.equal(pageViewLog.accept_language, "zh-CN,zh;q=0.9");
  assert.equal(pageViewLog.request_id, "request-page-view-001");
  assert.equal(pageViewLog.details.viewport, "1440×900");
  const updateLog = accessLogData.logs.find((item) => item.event_type === "submission_updated");
  assert.equal(updateLog.details.submissionTitle, "A useful suggestion");
  assert.ok(
    updateLog.details.changes.some(
      (change) => change.field === "developer_reply" &&
        change.before === "Planned for the next version." &&
        change.after === "Confirmed for the next release."
    ),
    "feedback update logs must retain field-level before and after values"
  );
  assert.ok(
    updateLog.details.changes.some(
      (change) => change.field === "like_count" && change.before !== 37 && change.after === 37
    ),
    "feedback update logs must identify numeric changes"
  );

  const refreshedPublicResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/public")
  );
  assert.equal(refreshedPublicResponse.status, 200);
  const refreshedPublicData = await refreshedPublicResponse.json();
  assert.equal(refreshedPublicData.suggestions[0].developer_reply, "Confirmed for the next release.");

  const declineResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/submissions", {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({
        id: "11111111-1111-4111-8111-111111111111",
        status: "declined",
        is_public: true
      })
    })
  );
  assert.equal(declineResponse.status, 200);
  const declineData = await declineResponse.json();
  assert.equal(declineData.submission.status, "declined");
  assert.equal(declineData.submission.is_public, false, "non-accepted reviews must not remain public");

  const hiddenPublicResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/public")
  );
  const hiddenPublicData = await hiddenPublicResponse.json();
  assert.equal(hiddenPublicData.suggestions.length, 0);

  const seededPreviewResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/previews", {
      headers: { cookie: adminCookie.split(";")[0] }
    })
  );
  assert.equal(seededPreviewResponse.status, 200);
  const seededPreviewData = await seededPreviewResponse.json();
  assert.equal(seededPreviewData.previews[0].version, "v2.1");
  assert.equal(seededPreviewData.previews[0].status, "published");
  assert.equal(releasePreviewRows.length, 1, "an empty preview database should be initialized once");

  const previewUpdateResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/previews", {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({
        id: seededPreviewData.previews[0].id,
        version: "v2.1",
        body_zh: "保留并更新当前预告",
        body_en: "Keep and update the current preview",
        target_date: "",
        status: "published"
      })
    })
  );
  assert.equal(previewUpdateResponse.status, 200);
  const previewUpdateData = await previewUpdateResponse.json();
  assert.equal(previewUpdateData.preview.body_zh, "保留并更新当前预告");
  assert.equal(releasePreviewRows.length, 1, "editing must update the current preview instead of duplicating it");

  calls.length = 0;
  const previewResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/previews", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({
        version: "v2.2 Preview",
        body_zh: "中文更新内容。",
        body_en: "English release notes.",
        body_zh_tw: "繁中更新內容。",
        body_ja: "日本語の更新内容。",
        target_date: "",
        status: "draft"
      })
    })
  );
  assert.equal(previewResponse.status, 201);
  const previewData = await previewResponse.json();
  assert.equal(previewData.preview.title_zh, "v2.2 Preview");
  assert.equal(previewData.preview.title_en, "v2.2 Preview");
  assert.equal(previewData.preview.body_zh, "中文更新内容。");
  assert.equal(previewData.preview.body_en, "English release notes.");
  assert.equal(previewData.preview.body_zh_tw, "繁中更新內容。");
  assert.equal(previewData.preview.body_ja, "日本語の更新内容。");
  assert.equal(previewData.preview.target_date, null);

  const publishedOnlyResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/previews", {
      headers: { cookie: adminCookie.split(";")[0] }
    })
  );
  const publishedOnlyData = await publishedOnlyResponse.json();
  assert.equal(publishedOnlyData.previews.length, 1, "the admin history must only list previews published to the public site");
  assert.equal(publishedOnlyData.drafts.length, 1, "saved drafts remain available through the compact draft controls");

  const publishSecondPreviewResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/previews", {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminCookie.split(";")[0]
      },
      body: JSON.stringify({ id: previewData.preview.id, status: "published" })
    })
  );
  assert.equal(publishSecondPreviewResponse.status, 200);
  const multiplePublicPreviews = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/public")
  );
  assert.equal((await multiplePublicPreviews.json()).previews.length, 2, "every published preview must be returned to the public page");

  const form = new FormData();
  form.set("kind", "feature");
  form.set("nickname", "Tester");
  form.set("email", "tester@example.com");
  form.set("title", "Three file test");
  form.set("body", "This submission verifies the maximum attachment request count.");
  for (let index = 0; index < 3; index += 1) {
    form.append("attachments", new File([`file-${index}`], `file-${index}.png`, { type: "image/png" }));
  }
  calls.length = 0;
  const submissionResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/submissions", {
      method: "POST",
      headers: { Origin: "https://lyric-island.top" },
      body: form
    })
  );
  assert.equal(submissionResponse.status, 201);
  assert.equal(calls.length, 4, "three uploads plus one insert must use exactly four subrequests");
  assert.ok(
    JSON.parse(calls.at(-1).init.body).reviewer_note.includes('"submitted"'),
    "submission source metadata must be stored in the same insert"
  );
  assert.equal(
    JSON.parse(calls.at(-1).init.body).reward_status,
    "pending",
    "new submissions must default to a pending reward"
  );

  const submissionLogResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/access-logs", {
      headers: { cookie: adminCookie.split(";")[0] }
    })
  );
  const submissionLogData = await submissionLogResponse.json();
  assert.ok(
    submissionLogData.logs.some((item) => item.event_type === "submission_created"),
    "new Bug and suggestion submissions must appear in access logs"
  );
  assert.ok(
    calls.every((call) => !call.url.includes("/rest/v1/access_logs")),
    "logging must not depend on the missing access_logs table"
  );

  const crossOriginResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/login", {
      method: "POST",
      headers: {
        Origin: "https://attacker.example",
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ password: values.__ESA_ADMIN_PASSWORD__ })
    })
  );
  assert.equal(crossOriginResponse.status, 403);

  // ──────────────────────────────────────────────────────────────────────────
  // ── Promo Code API Tests ──
  // ──────────────────────────────────────────────────────────────────────────

  const promoBase = "https://lyric-island.top/api/incentives/admin/promo-codes";
  const adminAuthCookie = adminCookie.split(";")[0];

  // ── 1. Authentication tests ──

  const promoNoAuthGetResponse = await api.fetch(
    new Request(promoBase)
  );
  assert.equal(promoNoAuthGetResponse.status, 401, "GET promo codes without auth must return 401");

  const promoNoAuthAllocateResponse = await api.fetch(
    new Request(`${promoBase}/allocate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ assigned_name: "Test" })
    })
  );
  assert.equal(promoNoAuthAllocateResponse.status, 401, "POST allocate without auth must return 401");

  // ── 2. List & Stats tests ──

  // Empty list should work gracefully
  promoCodeRows = [];
  calls.length = 0;
  const promoListEmptyResponse = await api.fetch(
    new Request(promoBase, { headers: { cookie: adminAuthCookie } })
  );
  assert.equal(promoListEmptyResponse.status, 200);
  const promoListEmptyData = await promoListEmptyResponse.json();
  assert.ok(Array.isArray(promoListEmptyData.codes), "response must have codes array");
  assert.equal(promoListEmptyData.codes.length, 0, "empty database returns empty codes array");
  assert.equal(promoListEmptyData.page, 1, "default page is 1");
  assert.equal(promoListEmptyData.pageSize, 20, "default pageSize is 20");
  assert.equal(promoListEmptyData.total, 0);
  assert.ok(promoListEmptyData.stats !== undefined, "response must have stats object");

  // Seed some promo code data for subsequent tests
  promoCodeRows = [
    {
      id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
      code: "PROMO-TEST-001-XXXXX",
      microsoft_code_id: "MS-001",
      distribution_status: "available",
      microsoft_available: true,
      microsoft_redeemed: false,
      raw_order_id: "ORD-001",
      campaign: "summer-sale",
      assigned_to_name: null,
      assigned_to_email: null,
      assigned_channel: null,
      assigned_at: null,
      microsoft_expire_at: "2027-01-01T00:00:00.000Z",
      note: "Test code 1",
      created_at: "2026-08-01T00:00:00.000Z",
      updated_at: "2026-08-01T00:00:00.000Z"
    },
    {
      id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
      code: "PROMO-TEST-002-YYYYY",
      microsoft_code_id: "MS-002",
      distribution_status: "allocated",
      microsoft_available: true,
      microsoft_redeemed: false,
      raw_order_id: "ORD-002",
      campaign: "winter-promo",
      assigned_to_name: "Alice",
      assigned_to_email: "alice@example.com",
      assigned_channel: "discord",
      assigned_at: "2026-08-05T00:00:00.000Z",
      microsoft_expire_at: "2027-06-01T00:00:00.000Z",
      note: "Test code 2",
      created_at: "2026-08-02T00:00:00.000Z",
      updated_at: "2026-08-05T00:00:00.000Z"
    },
    {
      id: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
      code: "PROMO-TEST-003-ZZZZZ",
      microsoft_code_id: "MS-003",
      distribution_status: "available",
      microsoft_available: false,
      microsoft_redeemed: false,
      raw_order_id: "ORD-003",
      campaign: "summer-sale",
      assigned_to_name: null,
      assigned_to_email: null,
      assigned_channel: null,
      assigned_at: null,
      microsoft_expire_at: "2027-03-01T00:00:00.000Z",
      note: "Another available code",
      created_at: "2026-08-03T00:00:00.000Z",
      updated_at: "2026-08-03T00:00:00.000Z"
    }
  ];

  // List with seeded data
  calls.length = 0;
  const promoListResponse = await api.fetch(
    new Request(promoBase, { headers: { cookie: adminAuthCookie } })
  );
  assert.equal(promoListResponse.status, 200);
  const promoListData = await promoListResponse.json();
  assert.equal(promoListData.codes.length, 3, "should return all 3 seeded codes");
  assert.equal(promoListData.total, 3);
  assert.ok(promoListData.stats, "stats object must be present");

  // Pagination test
  const promoPage1Response = await api.fetch(
    new Request(`${promoBase}?page=1&pageSize=2`, { headers: { cookie: adminAuthCookie } })
  );
  assert.equal(promoPage1Response.status, 200);
  const promoPage1Data = await promoPage1Response.json();
  assert.equal(promoPage1Data.codes.length, 2, "page 1 with pageSize=2 returns 2 items");
  assert.equal(promoPage1Data.page, 1);
  assert.equal(promoPage1Data.pageSize, 2);
  assert.equal(promoPage1Data.total, 3);

  const promoPage2Response = await api.fetch(
    new Request(`${promoBase}?page=2&pageSize=2`, { headers: { cookie: adminAuthCookie } })
  );
  assert.equal(promoPage2Response.status, 200);
  const promoPage2Data = await promoPage2Response.json();
  assert.equal(promoPage2Data.codes.length, 1, "page 2 with pageSize=2 returns remaining 1 item");

  // Status filter test
  const promoStatusResponse = await api.fetch(
    new Request(`${promoBase}?status=available`, { headers: { cookie: adminAuthCookie } })
  );
  assert.equal(promoStatusResponse.status, 200);
  const promoStatusData = await promoStatusResponse.json();
  assert.equal(promoStatusData.codes.length, 2, "status=available filters to 2 codes");
  assert.ok(
    promoStatusData.codes.every((c) => c.distribution_status === "available"),
    "all returned codes must match the status filter"
  );

  // Search query test
  const promoSearchResponse = await api.fetch(
    new Request(`${promoBase}?search=alice`, { headers: { cookie: adminAuthCookie } })
  );
  assert.equal(promoSearchResponse.status, 200);
  const promoSearchData = await promoSearchResponse.json();
  assert.equal(promoSearchData.codes.length, 1, "search for 'alice' finds 1 code");
  assert.equal(promoSearchData.codes[0].assigned_to_name, "Alice");

  // ── 3. Import tests ──

  // Valid import
  promoCodeBulkImportResult = { created: 5, updated: 1, unchanged: 2 };
  const promoImportResponse = await api.fetch(
    new Request(promoBase, {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({
        rows: [
          { microsoft_code_id: "MS-NEW-001", code: "NEW-CODE-001", microsoft_available: true },
          { microsoft_code_id: "MS-NEW-002", code: "NEW-CODE-002", microsoft_available: true }
        ],
        orderInfo: { order_id: "ORD-IMPORT-1" }
      })
    })
  );
  assert.equal(promoImportResponse.status, 200);
  const promoImportData = await promoImportResponse.json();
  assert.equal(promoImportData.created, 5);
  assert.equal(promoImportData.updated, 1);
  assert.equal(promoImportData.unchanged, 2);

  // Import without same-origin → 403
  const promoImportCrossOriginResponse = await api.fetch(
    new Request(promoBase, {
      method: "POST",
      headers: {
        Origin: "https://attacker.example",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ rows: [] })
    })
  );
  assert.equal(promoImportCrossOriginResponse.status, 403, "import from wrong origin must return 403");

  // ── 4. Preview tests ──

  const promoPreviewResponse = await api.fetch(
    new Request(`${promoBase}/preview`, {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({
        rows: [
          { microsoft_code_id: "MS-001", microsoft_available: true, microsoft_redeemed: false },
          { microsoft_code_id: "MS-NEW-PREVIEW", microsoft_available: true, microsoft_redeemed: false }
        ]
      })
    })
  );
  assert.equal(promoPreviewResponse.status, 200);
  const promoPreviewData = await promoPreviewResponse.json();
  assert.equal(promoPreviewData.total_detected, 2);
  assert.ok(typeof promoPreviewData.new_count === "number", "preview must include new_count");
  assert.ok(typeof promoPreviewData.existing_count === "number", "preview must include existing_count");
  assert.ok(typeof promoPreviewData.unchanged_count === "number", "preview must include unchanged_count");
  assert.ok(Array.isArray(promoPreviewData.errors), "preview must include errors array");
  assert.ok(Array.isArray(promoPreviewData.warnings), "preview must include warnings array");

  // Preview without auth → 401
  const promoPreviewNoAuthResponse = await api.fetch(
    new Request(`${promoBase}/preview`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ rows: [] })
    })
  );
  assert.equal(promoPreviewNoAuthResponse.status, 401, "preview without auth must return 401");

  // ── 5. Allocate tests ──

  // Allocate with no available code → 409
  promoCodeAllocateResult = null;
  const promoAllocateEmptyResponse = await api.fetch(
    new Request(`${promoBase}/allocate`, {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ assigned_name: "Bob", assigned_email: "bob@example.com", assigned_channel: "email", campaign: "test" })
    })
  );
  assert.equal(promoAllocateEmptyResponse.status, 409, "allocate with no inventory must return 409");

  // Allocate with available code → 200
  promoCodeAllocateResult = {
    id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    code: "PROMO-TEST-001-XXXXX",
    distribution_status: "allocated",
    assigned_to_name: "Bob",
    assigned_to_email: "bob@example.com",
    assigned_channel: "email",
    campaign: "test"
  };
  calls.length = 0;
  const promoAllocateResponse = await api.fetch(
    new Request(`${promoBase}/allocate`, {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ assigned_name: "Bob", assigned_email: "bob@example.com", assigned_channel: "email", campaign: "test" })
    })
  );
  assert.equal(promoAllocateResponse.status, 200);
  const promoAllocateData = await promoAllocateResponse.json();
  assert.equal(promoAllocateData.id, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
  assert.equal(promoAllocateData.assigned_to_name, "Bob");

  // Allocate without auth → 401 (already tested above, verify allocate cross-origin)
  const promoAllocateCrossOriginResponse = await api.fetch(
    new Request(`${promoBase}/allocate`, {
      method: "POST",
      headers: {
        Origin: "https://attacker.example",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ assigned_name: "Eve" })
    })
  );
  assert.equal(promoAllocateCrossOriginResponse.status, 403, "allocate from wrong origin must return 403");

  // ── 6. Export tests ──

  const promoExportResponse = await api.fetch(
    new Request(`${promoBase}/export`, {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ filters: {}, includeFullCode: false })
    })
  );
  assert.equal(promoExportResponse.status, 200);
  const exportContentType = promoExportResponse.headers.get("Content-Type");
  assert.match(exportContentType, /text\/plain/, "export must return text/csv content");
  const exportCsv = await promoExportResponse.text();
  assert.ok(exportCsv.includes("Code"), "CSV must contain header row with 'Code'");
  assert.ok(exportCsv.includes("Distribution Status"), "CSV must contain 'Distribution Status' header");

  // Export without auth → 401
  const promoExportNoAuthResponse = await api.fetch(
    new Request(`${promoBase}/export`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ filters: {} })
    })
  );
  assert.equal(promoExportNoAuthResponse.status, 401, "export without auth must return 401");

  // ── 7. Detail tests ──

  // Detail for existing code
  const promoDetailResponse = await api.fetch(
    new Request(`${promoBase}/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa`, {
      headers: { cookie: adminAuthCookie }
    })
  );
  assert.equal(promoDetailResponse.status, 200);
  const promoDetailData = await promoDetailResponse.json();
  assert.equal(promoDetailData.code.id, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
  assert.equal(promoDetailData.code.code, "PROMO-TEST-001-XXXXX");
  assert.ok(Array.isArray(promoDetailData.logs), "detail must include logs array");

  // Detail for non-existent code → 404
  const promoDetailMissingResponse = await api.fetch(
    new Request(`${promoBase}/ffffffff-ffff-4fff-8fff-ffffffffffff`, {
      headers: { cookie: adminAuthCookie }
    })
  );
  assert.equal(promoDetailMissingResponse.status, 404, "detail for missing code must return 404");

  // Detail without auth → 401
  const promoDetailNoAuthResponse = await api.fetch(
    new Request(`${promoBase}/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa`)
  );
  assert.equal(promoDetailNoAuthResponse.status, 401, "detail without auth must return 401");

  // ── 8. Update tests ──

  // Single update with valid fields
  promoCodeLogRows = [];
  const promoUpdateResponse = await api.fetch(
    new Request(promoBase, {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({
        id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        changes: { note: "Updated via test", campaign: "updated-campaign" }
      })
    })
  );
  assert.equal(promoUpdateResponse.status, 200);
  const promoUpdateData = await promoUpdateResponse.json();
  assert.equal(promoUpdateData.note, "Updated via test");
  assert.equal(promoUpdateData.campaign, "updated-campaign");
  assert.ok(promoCodeLogRows.length >= 1, "update must create an audit log entry");
  assert.equal(promoCodeLogRows.at(-1).action, "EDIT");

  // Update non-existent code → 404
  const promoUpdateMissingResponse = await api.fetch(
    new Request(promoBase, {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({
        id: "ffffffff-ffff-4fff-8fff-ffffffffffff",
        changes: { note: "no such code" }
      })
    })
  );
  assert.equal(promoUpdateMissingResponse.status, 404, "update for missing code must return 404");

  // Update without id → 400
  const promoUpdateNoIdResponse = await api.fetch(
    new Request(promoBase, {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ changes: { note: "no id provided" } })
    })
  );
  assert.equal(promoUpdateNoIdResponse.status, 400, "update without id must return 400");

  // Batch update with mass assignment attempt → only allowed fields applied
  const promoBatchUpdateResponse = await api.fetch(
    new Request(promoBase, {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({
        ids: ["aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "cccccccc-cccc-4ccc-8ccc-cccccccccccc"],
        changes: {
          note: "Batch updated",
          campaign: "batch-campaign",
          assigned_channel: "email",
          distribution_status: "redeemed",
          code: "HACKED-CODE"
        }
      })
    })
  );
  assert.equal(promoBatchUpdateResponse.status, 200);
  const promoBatchUpdateData = await promoBatchUpdateResponse.json();
  assert.equal(promoBatchUpdateData.updated, 2, "batch update must report 2 updated codes");
  // Verify that disallowed fields (distribution_status, code) were NOT applied
  const codeRow = promoCodeRows.find((r) => r.id === "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
  assert.equal(codeRow.note, "Batch updated", "allowed field 'note' must be updated");
  assert.equal(codeRow.campaign, "batch-campaign", "allowed field 'campaign' must be updated");
  assert.equal(codeRow.assigned_channel, "email", "allowed field 'assigned_channel' must be updated");
  assert.notEqual(codeRow.distribution_status, "redeemed", "disallowed field 'distribution_status' must NOT be updated");
  assert.notEqual(codeRow.code, "HACKED-CODE", "disallowed field 'code' must NOT be updated");

  // Batch update with no valid fields → 400
  const promoBatchNoFieldsResponse = await api.fetch(
    new Request(promoBase, {
      method: "PATCH",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({
        ids: ["aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"],
        changes: { distribution_status: "redeemed" }
      })
    })
  );
  assert.equal(promoBatchNoFieldsResponse.status, 400, "batch update with only disallowed fields must return 400");

  // Update cross-origin → 403
  const promoUpdateCrossOriginResponse = await api.fetch(
    new Request(promoBase, {
      method: "PATCH",
      headers: {
        Origin: "https://attacker.example",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", changes: { note: "xss" } })
    })
  );
  assert.equal(promoUpdateCrossOriginResponse.status, 403, "update from wrong origin must return 403");

  // ── 9. Delete tests ──

  // Delete available code → 200
  promoCodeLogRows = [];
  const promoDeleteResponse = await api.fetch(
    new Request(promoBase, {
      method: "DELETE",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ id: "cccccccc-cccc-4ccc-8ccc-cccccccccccc" })
    })
  );
  assert.equal(promoDeleteResponse.status, 200);
  const promoDeleteData = await promoDeleteResponse.json();
  assert.equal(promoDeleteData.ok, true);
  assert.ok(promoCodeLogRows.length >= 1, "delete must create an audit log entry");
  assert.equal(promoCodeLogRows.at(-1).action, "DELETE");

  // Verify the code was actually removed from mock
  assert.equal(
    promoCodeRows.find((r) => r.id === "cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
    undefined,
    "deleted code must no longer exist"
  );

  // Delete non-existent code → 404
  const promoDeleteMissingResponse = await api.fetch(
    new Request(promoBase, {
      method: "DELETE",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ id: "ffffffff-ffff-4fff-8fff-ffffffffffff" })
    })
  );
  assert.equal(promoDeleteMissingResponse.status, 404, "delete for missing code must return 404");

  // Delete without id → 400
  const promoDeleteNoIdResponse = await api.fetch(
    new Request(promoBase, {
      method: "DELETE",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({})
    })
  );
  assert.equal(promoDeleteNoIdResponse.status, 400, "delete without id must return 400");

  // Delete allocated code → 400 (only available codes can be deleted)
  const promoDeleteAllocatedResponse = await api.fetch(
    new Request(promoBase, {
      method: "DELETE",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb" })
    })
  );
  assert.equal(promoDeleteAllocatedResponse.status, 400, "delete for non-available code must return 400");

  // Delete cross-origin → 403
  const promoDeleteCrossOriginResponse = await api.fetch(
    new Request(promoBase, {
      method: "DELETE",
      headers: {
        Origin: "https://attacker.example",
        "Content-Type": "application/json",
        cookie: adminAuthCookie
      },
      body: JSON.stringify({ id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa" })
    })
  );
  assert.equal(promoDeleteCrossOriginResponse.status, 403, "delete from wrong origin must return 403");

  // Reset promo code state
  promoCodeRows = [];
  promoCodeLogRows = [];
  promoCodeAllocateResult = null;
  promoCodeBulkImportResult = null;
} finally {
  globalThis.fetch = originalFetch;
  await rm(buildDirectory, { recursive: true, force: true });
}

console.log("ESA API tests passed");
