import assert from "node:assert/strict";
import { createHmac } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import ts from "typescript";

const root = resolve(import.meta.dirname, "..");
const temp = await mkdtemp(join(tmpdir(), "lyrichover-sol-web-"));
const secret = "synthetic-sol-web-session-secret";
const values = {
  SUPABASE_URL: "https://qa.invalid",
  SUPABASE_SERVICE_ROLE_KEY: "sb_test_local_only",
  SUPABASE_STORAGE_BUCKET: "qa",
  ADMIN_PASSWORD: "synthetic-password",
  ADMIN_SESSION_SECRET: secret,
  DEEPSEEK_API_KEY: "",
  DEEPSEEK_BASE_URL: "https://qa.invalid",
  DEEPSEEK_MODEL: "qa",
  FEATURE_CONTENT_JSON: await readFile(resolve(root, "data/feature-content-default.json"), "utf8"),
  RELEASE_PREVIEW_JSON: await readFile(resolve(root, "data/release-preview-default.json"), "utf8")
};

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json" } });
}

function reviewMeta(isPublic) {
  return `[[lyric-island-review:v1]]${JSON.stringify({ reply: "", flagged: false, public: isPublic })}`;
}

function suggestion(index, { status = "accepted", isPublic = true } = {}) {
  return {
    id: `00000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    kind: "feature",
    nickname: `Synthetic ${index}`,
    email: `private-${index}@example.invalid`,
    title: `Suggestion ${index}`,
    body: "Synthetic public suggestion body.",
    status,
    reviewer_note: reviewMeta(isPublic),
    like_count: 0,
    attachments: [],
    created_at: `2026-01-01T00:00:${String(index % 60).padStart(2, "0")}Z`,
    updated_at: `2026-01-01T00:00:${String(index % 60).padStart(2, "0")}Z`
  };
}

let apiSource = await readFile(resolve(root, "esa", "api.js"), "utf8");
for (const [key, value] of Object.entries(values)) {
  apiSource = apiSource.replaceAll(JSON.stringify(`__ESA_${key}__`), JSON.stringify(value));
}
const apiEntry = resolve(temp, "api.mjs");
await writeFile(apiEntry, apiSource, "utf8");
const api = (await import(pathToFileURL(apiEntry).href)).default;

const originalFetch = globalThis.fetch;
let handler = async () => { throw new Error("No synthetic handler selected"); };
globalThis.fetch = async (input, init = {}) => {
  const url = new URL(String(input));
  assert.equal(url.origin, "https://qa.invalid", "tests must never call a real service");
  return handler(url, init);
};

async function readPublicSuggestions(rows) {
  const queries = [];
  handler = async (url) => {
    if (url.pathname === "/rest/v1/incentive_submissions") {
      queries.push(url);
      assert.equal(url.searchParams.get("status"), "eq.accepted");
      assert.equal(url.searchParams.get("order"), "updated_at.desc,id.desc");
      const accepted = rows.filter((row) => row.status === "accepted");
      assert.equal(url.searchParams.has("offset"), false, "public paging must use a stable keyset cursor");
      const cursorId = url.searchParams.get("or")?.match(/id\.lt\.([^\)]+)/)?.[1];
      const cursorIndex = cursorId ? accepted.findIndex((row) => row.id === cursorId) : -1;
      const offset = cursorIndex >= 0 ? cursorIndex + 1 : 0;
      const limit = Number(url.searchParams.get("limit") || accepted.length);
      return json(accepted.slice(offset, offset + limit));
    }
    if (url.pathname === "/rest/v1/release_previews") return json([]);
    if (url.pathname === "/rest/v1/incentive_likes") return json([]);
    throw new Error(`Unexpected public query: ${url}`);
  };
  const response = await api.fetch(new Request("https://site.invalid/api/incentives/public"));
  assert.equal(response.status, 200);
  return { body: await response.json(), queries };
}

try {
  // W01: status filtering happens in PostgREST, while accepted/private rows are
  // paged with a deterministic tie-break until 24 public rows or exhaustion.
  let result = await readPublicSuggestions([
    ...Array.from({ length: 100 }, (_, i) => suggestion(i + 1, { status: "pending" })),
    suggestion(1001)
  ]);
  assert.equal(result.body.suggestions.length, 1, "pending rows must not hide an older public row");

  result = await readPublicSuggestions([
    ...Array.from({ length: 100 }, (_, i) => suggestion(i + 1, { isPublic: false })),
    suggestion(1001)
  ]);
  assert.equal(result.body.suggestions.length, 1, "accepted/private rows must not hide an older public row");
  assert.equal(result.queries.length, 2, "private rows must trigger the next stable page");

  result = await readPublicSuggestions([
    ...Array.from({ length: 23 }, (_, i) => suggestion(i + 1)),
    ...Array.from({ length: 77 }, (_, i) => suggestion(i + 101, { isPublic: false })),
    suggestion(1001)
  ]);
  assert.equal(result.body.suggestions.length, 24, "public rows spanning pages must fill the public limit");
  assert.ok(result.body.suggestions.every((row) => !("reviewer_note" in row) && !("status" in row) && !("email" in row)));

  result = await readPublicSuggestions(Array.from({ length: 100 }, (_, i) => suggestion(i + 1, { isPublic: false })));
  assert.equal(result.body.suggestions.length, 0);
  assert.equal(result.queries.length, 2, "an exact full private page must check the following page");
  result = await readPublicSuggestions([]);
  assert.equal(result.body.suggestions.length, 0);

  // W02 API wiring: every like goes through the transactional RPC. The real
  // SQL transaction behavior is tested separately in a PostgreSQL engine.
  const voterTokens = new Set();
  let storedCount = 0;
  const likePaths = [];
  handler = async (url, init) => {
    likePaths.push(`${init.method || "GET"} ${url.pathname}`);
    assert.equal(url.pathname, "/rest/v1/rpc/toggle_incentive_like");
    const input = JSON.parse(init.body);
    const alreadyLiked = voterTokens.has(input.p_voter_token_hash);
    if (!alreadyLiked) {
      voterTokens.add(input.p_voter_token_hash);
      storedCount += 1;
    }
    return json([{ liked: true, like_count: storedCount, already_liked: alreadyLiked }]);
  };
  const like = (cookie) => api.fetch(new Request("https://site.invalid/api/incentives/likes", {
    method: "POST",
    headers: { origin: "https://site.invalid", "content-type": "application/json", cookie },
    body: JSON.stringify({ submissionId: "11111111-1111-4111-8111-111111111111" })
  }));
  const differentVoters = await Promise.all([like("lyric_island_voter=a"), like("lyric_island_voter=b")]);
  assert.ok(differentVoters.every((response) => response.status === 200));
  assert.equal(storedCount, 2);
  const repeated = await like("lyric_island_voter=a");
  assert.equal((await repeated.json()).already_liked, true);
  assert.equal(storedCount, 2);
  assert.ok(likePaths.every((path) => path === "POST /rest/v1/rpc/toggle_incentive_like"), "no unsafe REST fallback is allowed");

  handler = async (url) => {
    assert.equal(url.pathname, "/rest/v1/rpc/toggle_incentive_like");
    return json({ message: "Suggestion is not available for likes" }, 400);
  };
  assert.equal((await like("lyric_island_voter=private")).status, 500, "a private or pending target must not report success");

  // W03: the delete predicate is checked at mutation time and the audit uses
  // the returned deleted row. Zero returned rows mean conflict and no success audit.
  const payload = `${Math.floor(Date.now() / 1000) + 600}.synthetic-nonce`;
  const signature = createHmac("sha256", secret).update(payload).digest("hex");
  const adminCookie = `lyric_island_admin=${payload}.${signature}`;
  const deleteRequest = () => api.fetch(new Request("https://site.invalid/api/incentives/admin/promo-codes", {
    method: "DELETE",
    headers: { origin: "https://site.invalid", "content-type": "application/json", cookie: adminCookie },
    body: JSON.stringify({ id: "11111111-1111-4111-8111-111111111111" })
  }));
  let promoState = "available";
  let audits = 0;
  let conditionalDeleteSeen = false;
  let raceMode = "assign";
  handler = async (url, init) => {
    if (url.pathname === "/rest/v1/promo_codes" && !init.method) {
      const snapshot = promoState ? [{ id: "11111111-1111-4111-8111-111111111111", distribution_status: promoState }] : [];
      if (raceMode === "assign") promoState = "assigned";
      if (raceMode === "delete") promoState = null;
      return json(snapshot);
    }
    if (url.pathname === "/rest/v1/promo_codes" && init.method === "DELETE") {
      conditionalDeleteSeen = url.searchParams.get("distribution_status") === "eq.available";
      if (conditionalDeleteSeen && promoState === "available") {
        promoState = null;
        return json([{ id: "11111111-1111-4111-8111-111111111111", distribution_status: "available" }]);
      }
      return json([]);
    }
    if (url.pathname === "/rest/v1/promo_code_logs") {
      audits += 1;
      return json([]);
    }
    throw new Error(`Unexpected promo query: ${url}`);
  };
  let response = await deleteRequest();
  assert.equal(response.status, 409);
  assert.equal(promoState, "assigned");
  assert.equal(audits, 0);
  assert.equal(conditionalDeleteSeen, true);

  promoState = "available";
  raceMode = "delete";
  audits = 0;
  response = await deleteRequest();
  assert.equal(response.status, 409, "a concurrent delete must not create a false success audit");
  assert.equal(audits, 0);

  promoState = "available";
  raceMode = "none";
  audits = 0;
  response = await deleteRequest();
  assert.equal(response.status, 200);
  assert.equal(audits, 1);

  promoState = "assigned";
  raceMode = "none";
  audits = 0;
  response = await deleteRequest();
  assert.equal(response.status, 400);
  assert.equal(audits, 0);

  // W05: admin storage remains verbatim; the ESA public response redacts every
  // localized render field while preserving player compatibility wording.
  const feature = JSON.parse(values.FEATURE_CONTENT_JSON);
  for (const key of ["label_zh", "label_en", "label_zh_tw", "label_ja"]) feature.summary[key] = `LRCLIB ${key}`;
  for (const key of ["items_zh", "items_en", "items_zh_tw", "items_ja"]) feature.summary[key] = [`LRCLIB ${key}`];
  for (const key of ["title_zh", "title_en", "title_zh_tw", "title_ja", "body_zh", "body_en", "body_zh_tw", "body_ja"]) feature.sections[0][key] = `LRCLIB ${key}`;
  for (const key of ["items_zh", "items_en", "items_zh_tw", "items_ja"]) feature.sections[0][key] = [`LRCLIB ${key}`];
  feature.sections.push({
    id: "player-compatibility", release_version: "v3.2.36", visible: true,
    title_zh: "播放器兼容", title_en: "Player compatibility", title_zh_tw: "播放器相容", title_ja: "プレーヤー互換性",
    body_zh: "兼容 QQ 音乐、网易云音乐和酷狗播放器。", body_en: "Supports QQ Music, NetEase and Kugou Music players.",
    body_zh_tw: "相容 QQ 音樂、網易雲音樂和酷狗播放器。", body_ja: "QQ Music、NetEase、Kugou Music プレーヤーに対応。",
    items_zh: [], items_en: [], items_zh_tw: [], items_ja: []
  });
  handler = async (url) => {
    if (url.pathname === "/rest/v1/release_previews") {
      return json([{ id: "feature-row", highlights_zh: feature }]);
    }
    throw new Error(`Unexpected feature query: ${url}`);
  };
  const adminResponse = await api.fetch(new Request("https://site.invalid/api/incentives/admin/features", { headers: { cookie: adminCookie } }));
  assert.equal(adminResponse.status, 200);
  assert.match(JSON.stringify(await adminResponse.json()), /LRCLIB/, "admin content must preserve source evidence");
  const publicResponse = await api.fetch(new Request("https://site.invalid/api/features"));
  assert.equal(publicResponse.status, 200);
  const publicContent = (await publicResponse.json()).content;
  assert.doesNotMatch(JSON.stringify(publicContent), /LRCLIB/i);
  assert.match(publicContent.sections.at(-1).body_zh, /QQ 音乐、网易云音乐和酷狗播放器/);
  assert.match(publicContent.sections.at(-1).body_en, /QQ Music, NetEase and Kugou Music players/);

  // Execute the real client-side TypeScript filter after replacing only its
  // compile-time imports with the same bundled fallback fixture.
  let featureSource = await readFile(resolve(root, "data", "feature-content.ts"), "utf8");
  featureSource = featureSource
    .replace('import defaultContentJson from "@/data/feature-content-default.json";', `const defaultContentJson = ${values.FEATURE_CONTENT_JSON};`)
    .replace(/import type[^;]+;\r?\n/g, "");
  const featureEntry = resolve(temp, "feature-content.mjs");
  await writeFile(featureEntry, ts.transpileModule(featureSource, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText);
  const { FEATURE_CONTENT_SECTION_LIMIT, parseFeatureContentModules, parseFeatureContentText, publicFeatureContent, sanitizeFeatureContent } = await import(pathToFileURL(featureEntry).href);
  const clientContent = publicFeatureContent(sanitizeFeatureContent(feature));
  assert.doesNotMatch(JSON.stringify(clientContent), /LRCLIB/i, "client fail-closed filter must cover every localized field");
  assert.match(clientContent.sections.at(-1).body_zh, /QQ 音乐、网易云音乐和酷狗播放器/);
  assert.equal(FEATURE_CONTENT_SECTION_LIMIT, 120);
  const expandedFeatureContent = sanitizeFeatureContent({
    ...feature,
    sections: Array.from({ length: 32 }, (_, index) => ({
      ...feature.sections[0],
      id: `feature-capacity-${index + 1}`,
      title_zh: `容量测试 ${index + 1}`,
      title_en: `Capacity test ${index + 1}`
    }))
  });
  assert.equal(expandedFeatureContent.sections.length, 32, "feature content must not be truncated at the former 30-section limit");
  assert.deepEqual(
    parseFeatureContentText("标题：逐字跟随\n描述：歌词随着演唱进度逐字呈现。\n分条：\n- 支持平滑高亮\n2. 跳转后自动对齐"),
    {
      title: "逐字跟随",
      description: "歌词随着演唱进度逐字呈现。",
      items: ["支持平滑高亮", "跳转后自动对齐"]
    }
  );
  assert.deepEqual(
    parseFeatureContentText("歌词坞｜让歌词显示在任务栏。\n• 支持左右任务栏\n• 支持多显示器"),
    {
      title: "歌词坞",
      description: "让歌词显示在任务栏。",
      items: ["支持左右任务栏", "支持多显示器"]
    }
  );
  assert.deepEqual(
    parseFeatureContentModules("标题：逐字跟随\n描述：歌词随演唱进度逐字呈现。\n分条：\n- 平滑高亮\n\n标题：歌词坞\n描述：让歌词显示在任务栏。\n分条：\n- 支持多显示器"),
    [
      { title: "逐字跟随", description: "歌词随演唱进度逐字呈现。", items: ["平滑高亮"] },
      { title: "歌词坞", description: "让歌词显示在任务栏。", items: ["支持多显示器"] }
    ]
  );
  assert.deepEqual(
    parseFeatureContentModules("省电模式 | 降低后台资源占用。\n- 自动降低刷新频率\n\n歌词坞｜让歌词显示在任务栏。\n• 支持左右任务栏"),
    [
      { title: "省电模式", description: "降低后台资源占用。", items: ["自动降低刷新频率"] },
      { title: "歌词坞", description: "让歌词显示在任务栏。", items: ["支持左右任务栏"] }
    ]
  );
  assert.deepEqual(
    parseFeatureContentModules("标题：省电模式\n描述：降低资源占用。\n---\n标题：歌词坞\n描述：显示在任务栏。"),
    [
      { title: "省电模式", description: "降低资源占用。", items: [] },
      { title: "歌词坞", description: "显示在任务栏。", items: [] }
    ]
  );
  assert.deepEqual(
    parseFeatureContentModules("标题：省电模式\n\n描述：降低资源占用。\n\n分条：\n- 自动降低刷新频率"),
    [{ title: "省电模式", description: "降低资源占用。", items: ["自动降低刷新频率"] }]
  );
  const pastedFeatureModules = parseFeatureContentModules([
    "标题：更懂每一首歌\\",
    "&#x20;描述：采用新的歌词匹配算法，更准确地找到与当前歌曲对应的歌词。\\",
    "&#x20;分条：",
    "",
    "- 综合匹配成功率提高 60%",
    "- 同时识别歌名、歌手与歌曲版本",
    "- 首个结果不够理想时，继续寻找更合适的歌词",
    "- 减少现场版、混音版与其他版本的错误匹配",
    "",
    "---",
    "",
    "标题：轮廓，由你定义\\",
    "&#x20;描述：从顶部形状到屏幕距离，让歌词岛更贴近你的桌面。\\",
    "&#x20;分条：",
    "",
    "- 在凹角与凸角两种外观之间自由切换",
    "- 分别调整不同外观对应的圆角半径",
    "- 调整凸角模式下的屏幕边缘距离",
    "",
    "---",
    "",
    "标题：字体，各有风格\\",
    "&#x20;描述：为歌词岛与歌词坞选择更适合各自位置的文字外观。\\",
    "&#x20;分条：",
    "",
    "- 分别设置歌词岛与歌词坞的字体和字号",
    "- 中文与英文字体可以独立选择",
    "- 调整整体大小时，保留各模块原有的字号比例",
    "",
    "---",
    "",
    "标题：每个字，都更清晰\\",
    "&#x20;描述：从透明背景到高分屏，歌词始终清楚、自然、舒展。\\",
    "&#x20;分条：",
    "",
    "- 改善透明背景下的文字清晰度",
    "- 针对不同 DPI 对齐物理像素，减少文字毛边",
    "- 长歌词在显示区域两侧自然渐隐",
    "- 避免大字号、宽笔画与英文字符被裁切",
    "- 修复字体或显示宽度变化后不居中的问题",
    "",
    "---",
    "",
    "标题：每一次调整，都更流畅\\",
    "&#x20;描述：重新打磨模块编辑与外观切换，让变化自然衔接。\\",
    "&#x20;分条：",
    "",
    "- 统一模块拖入、拖出与重新排序的动画",
    "- 拖动时清楚显示即将插入的位置",
    "- 布局交接时保持歌词岛尺寸稳定，修复抖动闪烁的问题",
    "- 凹凸角切换时同步调整形状、尺寸与位置",
    "",
    "---",
    "",
    "标题：稳定，藏在每个细节里\\",
    "&#x20;描述：修复高频操作中的崩溃，让日常使用更加可靠。\\",
    "&#x20;分条：",
    "",
    "- 设置保存失败时保留修改，并支持重新尝试",
    "- 修复右键歌词和拖动模块时可能发生的崩溃",
    "- 修复调整歌词宽度、分割线与布局时可能发生的崩溃"
  ].join("\n"));
  assert.deepEqual(
    pastedFeatureModules.map((module) => ({ title: module.title, description: module.description, itemCount: module.items.length })),
    [
      { title: "更懂每一首歌", description: "采用新的歌词匹配算法，更准确地找到与当前歌曲对应的歌词。", itemCount: 4 },
      { title: "轮廓，由你定义", description: "从顶部形状到屏幕距离，让歌词岛更贴近你的桌面。", itemCount: 3 },
      { title: "字体，各有风格", description: "为歌词岛与歌词坞选择更适合各自位置的文字外观。", itemCount: 3 },
      { title: "每个字，都更清晰", description: "从透明背景到高分屏，歌词始终清楚、自然、舒展。", itemCount: 5 },
      { title: "每一次调整，都更流畅", description: "重新打磨模块编辑与外观切换，让变化自然衔接。", itemCount: 4 },
      { title: "稳定，藏在每个细节里", description: "修复高频操作中的崩溃，让日常使用更加可靠。", itemCount: 3 }
    ],
    "the exact pasted rich-text export must import as six clean modules"
  );

  // W04: strict calendar/time components and the one legal never-expire sentinel.
  const parserSource = await readFile(resolve(root, "lib", "promo-code-tsv-parser.ts"), "utf8");
  const parserEntry = resolve(temp, "promo-code-tsv-parser.mjs");
  await writeFile(parserEntry, ts.transpileModule(parserSource, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText);
  const { parseTsv } = await import(pathToFileURL(parserEntry).href);
  const parseDate = (date) => parseTsv(`Promotional code\tCode ID\tExpiration date\nSYNTHETIC\tSYNTHETIC-ID\t${date}`);
  for (const invalid of ["2026-02-31", "2026/13/40", "2026-01-01T24:00:00", "2026/1/1 12:60", "2025-02-29", "0001/99/99"]) {
    assert.ok(parseDate(invalid).errors.length > 0, `${invalid} must be rejected`);
  }
  for (const valid of ["2024-02-29", "2026-09-07", "2026/9/7", "9/7/2026", "2026/9/7 3:04", "9/7/2026 3:04", "0001/1/1", "0001/01/01 00:00"]) {
    assert.equal(parseDate(valid).errors.length, 0, `${valid} must remain valid`);
  }

  console.log("Sol website behavior regressions passed: W01 W02 W03 W04 W05");
} finally {
  globalThis.fetch = originalFetch;
  await rm(temp, { recursive: true, force: true });
}
