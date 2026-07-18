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
  "__ESA_ADMIN_SESSION_SECRET__": "test-session-secret-with-at-least-32-characters"
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

function response(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" }
  });
}

globalThis.fetch = async (input, init = {}) => {
  const url = String(input);
  calls.push({ url, init });
  if (url.includes("/rest/v1/incentive_likes?")) {
    return response([{ submission_id: "11111111-1111-4111-8111-111111111111" }]);
  }
  if (url.includes("incentive_submissions?select=id,kind,nickname,title,body,created_at,like_count,attachments,reviewer_note")) {
    return response([
      {
        id: "11111111-1111-4111-8111-111111111111",
        kind: "feature",
        nickname: "Tester",
        title: "A useful suggestion",
        body: "This is a sufficiently detailed public suggestion.",
        created_at: "2026-07-18T00:00:00.000Z",
        like_count: 1,
        reviewer_note: '[[lyric-island-review:v1]]{"reply":"Planned for the next version.","flagged":false,"public":true}',
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
  if (url.includes("release_previews?select=*&status=eq.published")) {
    return response([]);
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
    return response([
      {
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
        like_count: 1,
        status: "accepted",
        reward_status: "not_eligible",
        reviewer_note: null,
        created_at: "2026-07-18T00:00:00.000Z",
        updated_at: "2026-07-18T00:00:00.000Z"
      }
    ]);
  }
  if (url.endsWith("/storage/v1/object/lyric-island-submissions/placeholder")) {
    return response({});
  }
  if (url.includes("/storage/v1/object/lyric-island-submissions/")) {
    return response({ Key: "uploaded" });
  }
  if (url.endsWith("/rest/v1/incentive_submissions") && init.method === "POST") {
    const body = JSON.parse(init.body);
    return response([{ ...body, status: "pending" }], 201);
  }
  if (url.endsWith("/rest/v1/release_previews") && init.method === "POST") {
    const body = JSON.parse(init.body);
    return response([{
      id: "22222222-2222-4222-8222-222222222222",
      ...body,
      created_at: "2026-07-18T00:00:00.000Z",
      updated_at: "2026-07-18T00:00:00.000Z"
    }], 201);
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
  assert.equal(calls.length, 4, "public API must stay within ESA's four-subrequest limit");
  assert.ok(
    calls.every((call) => call.init.headers.apikey === values.__ESA_SUPABASE_SERVICE_ROLE_KEY__),
    "every Supabase request must send the secret in the apikey header"
  );
  assert.ok(
    calls.every((call) => !("Authorization" in call.init.headers)),
    "opaque sb_secret_ keys must not be sent as bearer JWTs"
  );

  const loginResponse = await api.fetch(
    new Request("https://lyric-island.top/api/incentives/admin/login", {
      method: "POST",
      headers: {
        Origin: "https://lyric-island.top",
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ password: values.__ESA_ADMIN_PASSWORD__ })
    })
  );
  assert.equal(loginResponse.status, 200);
  const adminCookie = loginResponse.headers.get("set-cookie");
  assert.match(adminCookie, /HttpOnly; Secure; SameSite=Strict/);

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
  assert.equal(previewData.preview.target_date, null);

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
