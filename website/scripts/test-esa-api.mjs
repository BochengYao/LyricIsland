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
    return response(releasePreviewRows.filter((row) => row.status === "published"));
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
        targetLocales: ["en", "ja"],
        entries: [{ key: "preview.body", text: "支持自定义歌词岛形状" }]
      })
    })
  );
  assert.equal(translationResponse.status, 200);
  const translationData = await translationResponse.json();
  assert.equal(translationData.translations.en["preview.body"], "en:支持自定义歌词岛形状");
  assert.equal(translationData.translations.ja["preview.body"], "ja:支持自定义歌词岛形状");
  assert.equal(calls.length, 1, "translation must make exactly one server-side DeepSeek request");

  const managedFeatures = structuredClone(publicFeaturesData.content);
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
} finally {
  globalThis.fetch = originalFetch;
  await rm(buildDirectory, { recursive: true, force: true });
}

console.log("ESA API tests passed");
