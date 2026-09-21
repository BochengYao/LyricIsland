import type {
  ReleasePreviewFeature,
  ReleasePreviewFeatureGroup
} from "@/data/incentives-types";

export const RELEASE_PREVIEW_SCHEMA_VERSION = 3;
export const RELEASE_PREVIEW_PROGRESS_ANCHORS = [0, 10, 30, 50, 65, 80, 90, 95] as const;
export const RELEASE_PREVIEW_SLIDER_TESTING_INDEX = RELEASE_PREVIEW_PROGRESS_ANCHORS.length;
export const RELEASE_PREVIEW_SLIDER_READY_INDEX = RELEASE_PREVIEW_SLIDER_TESTING_INDEX + 1;
export const KNOWN_V32_NOTE_ZH = "新版本的主要功能已基本完成，目前正在进一步优化性能、功耗与长期运行体验，发布时间调整至本月内。";

export type StoredReleasePreviewFeatures = {
  schema_version: typeof RELEASE_PREVIEW_SCHEMA_VERSION;
  features: ReleasePreviewFeature[];
};

export type StoredReleasePreviewEnvelope = Array<StoredReleasePreviewFeatures | string>;

type PreviewContentSource = {
  id?: unknown;
  version?: unknown;
  note_zh?: unknown;
  note_en?: unknown;
  note_zh_tw?: unknown;
  note_ja?: unknown;
  features?: unknown;
  body_zh?: unknown;
  body_en?: unknown;
  body_zh_tw?: unknown;
  body_ja?: unknown;
  highlights_zh?: unknown;
  highlights_en?: unknown;
  highlights_zh_tw?: unknown;
  highlights_ja?: unknown;
};

export type NormalizedReleasePreviewContent = {
  note_zh: string;
  note_en: string;
  note_zh_tw: string;
  note_ja: string;
  features: ReleasePreviewFeature[];
};

export function splitPreviewItems(value: unknown): string[] {
  if (typeof value !== "string") return [];
  return value
    .split(/\r?\n|[；;]/)
    .map((item) => item.replace(/^\s*(?:[-–—*•·]|\d+[.)、])\s*/, "").trim())
    .filter(Boolean);
}

export function splitPreviewLines(value: unknown): string[] {
  if (typeof value !== "string") return [];
  return value
    .split(/\r?\n+/)
    .map((item) => item.replace(/^\s*(?:[-–—*•·]|\d+[.)、])\s*/, "").trim())
    .filter(Boolean);
}

function text(value: unknown, maxLength = 2_400) {
  return typeof value === "string" ? value.trim().slice(0, maxLength) : "";
}

function lines(value: unknown) {
  if (!Array.isArray(value)) return [];
  return value.map((item) => text(item)).filter(Boolean);
}

function safeProgress(value: unknown): number {
  if (typeof value !== "number" || !Number.isFinite(value)) return 0;
  const rounded = Math.min(100, Math.max(0, Math.round(value)));
  if (rounded === 100) return 100;
  return [...RELEASE_PREVIEW_PROGRESS_ANCHORS].reverse().find((anchor) => anchor <= rounded) ?? 0;
}

function legacyContent(title: string, description: string) {
  return title ? `${title} — ${description}` : description;
}

type SafeLegacyMigration = {
  title: string;
  description: string;
  displayGroup: ReleasePreviewFeatureGroup;
  targetVersion?: string;
};

const safeLegacyMigrations: Record<string, SafeLegacyMigration> = {
  "新增省电模式，进一步降低后台资源占用。": { title: "省电模式", description: "进一步降低后台资源占用。", displayGroup: "featured" },
  "进一步降低后台资源占用。": { title: "省电模式", description: "进一步降低后台资源占用。", displayGroup: "featured" },
  "新增逐字追踪，让歌词随演唱进度逐字呈现，带来更自然的跟唱体验。": { title: "逐字跟随", description: "歌词随着演唱进度逐字呈现，带来更自然的跟唱体验。", displayGroup: "featured" },
  "新增逐字跟随，让歌词随演唱进度逐字呈现，带来更自然的跟唱体验。": { title: "逐字跟随", description: "歌词随着演唱进度逐字呈现，带来更自然的跟唱体验。", displayGroup: "featured" },
  "歌词随着演唱进度逐字呈现，带来更自然的跟唱体验。": { title: "逐字跟随", description: "歌词随着演唱进度逐字呈现，带来更自然的跟唱体验。", displayGroup: "featured" },
  "新增歌词坞，让当前歌词直接呈现在 Windows 任务栏。": { title: "歌词坞", description: "让当前歌词直接呈现在 Windows 任务栏。", displayGroup: "featured" },
  "让当前歌词直接呈现在 Windows 任务栏。": { title: "歌词坞", description: "让当前歌词直接呈现在 Windows 任务栏。", displayGroup: "featured" },
  "新增重新匹配歌词，支持手动刷新并重新匹配当前歌词。": { title: "重新匹配歌词", description: "支持手动刷新并重新匹配当前歌词。", displayGroup: "featured" },
  "支持手动刷新并重新匹配当前歌词。": { title: "重新匹配歌词", description: "支持手动刷新并重新匹配当前歌词。", displayGroup: "featured" },
  "支持手动刷新并重新匹配歌词。": { title: "重新匹配歌词", description: "支持手动刷新并重新匹配当前歌词。", displayGroup: "featured" },
  "优化歌词匹配逻辑，减少歌词与当前歌曲不一致的情况。": { title: "歌词匹配", description: "优化歌词匹配逻辑，减少歌词与当前歌曲不一致的情况。", displayGroup: "improvement" },
  "支持全屏应用运行时自动隐藏歌词岛。": { title: "全屏体验", description: "支持全屏应用运行时自动隐藏歌词岛。", displayGroup: "improvement" },
  "接入更多歌词来源，进一步提升歌词与翻译的覆盖范围。": { title: "更多歌词来源", description: "接入更多歌词来源，进一步提升歌词与翻译的覆盖范围。", displayGroup: "improvement" },
  "持续优化性能、功耗与长期运行稳定性。": { title: "性能与稳定性", description: "持续优化性能、功耗与长期运行稳定性。", displayGroup: "improvement" },
  "支持更多歌词岛形状与自定义轮廓。": { title: "更多歌词岛形状", description: "支持更多歌词岛形状与自定义轮廓。", displayGroup: "future", targetVersion: "V3.3" },
  "更多歌词岛形状与自定义轮廓计划于 V3.3 带来。": { title: "更多歌词岛形状", description: "支持更多歌词岛形状与自定义轮廓。", displayGroup: "future", targetVersion: "V3.3" },
  "支持模块字体与主题色独立设置。": { title: "模块个性化", description: "支持模块字体与主题色独立设置。", displayGroup: "future", targetVersion: "V3.3" },
  "模块字体与主题色的独立设置计划于 V3.3 带来。": { title: "模块个性化", description: "支持模块字体与主题色独立设置。", displayGroup: "future", targetVersion: "V3.3" }
};

function legacyMigration(version: string, contentZh: string): SafeLegacyMigration | null {
  if (!/^v?3\.[23](?:\D|$)/i.test(version.trim())) return null;
  return safeLegacyMigrations[contentZh] ?? null;
}

function safeStage(value: unknown, progress: number): ReleasePreviewFeature["stage"] {
  if (progress !== 100) return "development";
  if (value === "ready") return "ready";
  return "testing";
}

function stableLegacyFeatureId(previewId: string, content: string, position: number) {
  let hash = 2166136261;
  const source = `${previewId}\u0000${content}\u0000${position}`;
  for (let index = 0; index < source.length; index += 1) {
    hash ^= source.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return `legacy-${(hash >>> 0).toString(36)}`;
}

function sanitizeFeature(value: unknown, fallbackOrder: number, previewVersion = ""): ReleasePreviewFeature | null {
  if (!value || typeof value !== "object") return null;
  const source = value as Record<string, unknown>;
  const id = text(source.id, 120);
  const legacyZh = text(source.content_zh);
  const legacyEn = text(source.content_en);
  const legacyZhTw = text(source.content_zh_tw);
  const legacyJa = text(source.content_ja);
  const migration = legacyMigration(previewVersion, legacyZh);
  const titleZh = text(source.title_zh, 180) || migration?.title || "";
  const titleEn = text(source.title_en, 180);
  const titleZhTw = text(source.title_zh_tw, 180);
  const titleJa = text(source.title_ja, 180);
  const descriptionZh = text(source.description_zh) || migration?.description || legacyZh;
  const descriptionEn = text(source.description_en) || legacyEn;
  const descriptionZhTw = text(source.description_zh_tw) || legacyZhTw;
  const descriptionJa = text(source.description_ja) || legacyJa;
  if (!id || (!descriptionZh && !descriptionEn && !descriptionZhTw && !descriptionJa)) return null;
  const rawOrder = typeof source.sort_order === "number" && Number.isFinite(source.sort_order)
    ? Math.round(source.sort_order)
    : fallbackOrder;
  const progress = safeProgress(source.progress);
  const requestedGroup = text(source.display_group, 40);
  const displayGroup: ReleasePreviewFeatureGroup = requestedGroup === "improvement" || requestedGroup === "future"
    ? requestedGroup
    : migration?.displayGroup ?? "featured";
  const targetVersion = displayGroup === "future"
    ? text(source.target_version, 40) || migration?.targetVersion || previewVersion
    : "";
  return {
    id,
    sort_order: Math.max(0, rawOrder),
    progress,
    stage: safeStage(source.stage, progress),
    display_group: displayGroup,
    target_version: targetVersion,
    title_zh: titleZh,
    title_en: titleEn,
    title_zh_tw: titleZhTw,
    title_ja: titleJa,
    description_zh: descriptionZh,
    description_en: descriptionEn,
    description_zh_tw: descriptionZhTw,
    description_ja: descriptionJa,
    content_zh: legacyZh || legacyContent(titleZh, descriptionZh),
    content_en: legacyEn || legacyContent(titleEn, descriptionEn),
    content_zh_tw: legacyZhTw || legacyContent(titleZhTw, descriptionZhTw),
    content_ja: legacyJa || legacyContent(titleJa, descriptionJa)
  };
}

function structuredFeatures(value: unknown, previewVersion: string) {
  if (!value || typeof value !== "object") return null;
  const candidate = Array.isArray(value)
    ? value.find((item) => Boolean(item) && typeof item === "object" && !Array.isArray(item))
    : value;
  if (!candidate || typeof candidate !== "object" || Array.isArray(candidate)) return null;
  const source = candidate as Record<string, unknown>;
  if ((source.schema_version !== 2 && source.schema_version !== RELEASE_PREVIEW_SCHEMA_VERSION) || !Array.isArray(source.features)) return null;
  const usedIds = new Set<string>();
  return source.features
    .map((feature, index) => sanitizeFeature(feature, index + 1, previewVersion))
    .filter((feature): feature is ReleasePreviewFeature => {
      if (!feature || usedIds.has(feature.id)) return false;
      usedIds.add(feature.id);
      return true;
    })
    .sort((left, right) => left.sort_order - right.sort_order)
    .map((feature, index) => ({ ...feature, sort_order: index + 1 }));
}

function featureFromLegacy(
  previewId: string,
  previewVersion: string,
  index: number,
  zh: string[],
  en: string[],
  zhTw: string[],
  ja: string[]
): ReleasePreviewFeature {
  const primary = zh[index] || en[index] || zhTw[index] || ja[index] || `feature-${index + 1}`;
  const migration = legacyMigration(previewVersion, zh[index] ?? "");
  const descriptionZh = migration?.description || zh[index] || "";
  const displayGroup = migration?.displayGroup ?? "featured";
  return {
    id: stableLegacyFeatureId(previewId, primary, index),
    sort_order: index + 1,
    progress: 0,
    stage: "development",
    display_group: displayGroup,
    target_version: displayGroup === "future" ? migration?.targetVersion || previewVersion : "",
    title_zh: migration?.title || "",
    title_en: "",
    title_zh_tw: "",
    title_ja: "",
    description_zh: descriptionZh,
    description_en: en[index] ?? "",
    description_zh_tw: zhTw[index] ?? "",
    description_ja: ja[index] ?? "",
    content_zh: zh[index] ?? "",
    content_en: en[index] ?? "",
    content_zh_tw: zhTw[index] ?? "",
    content_ja: ja[index] ?? ""
  };
}

export function normalizeReleasePreviewContent(source: PreviewContentSource): NormalizedReleasePreviewContent {
  const previewVersion = text(source.version, 40);
  const directFeatures = Array.isArray(source.features)
    ? source.features
      .map((feature, index) => sanitizeFeature(feature, index + 1, previewVersion))
      .filter((feature): feature is ReleasePreviewFeature => Boolean(feature))
      .sort((left, right) => left.sort_order - right.sort_order)
      .map((feature, index) => ({ ...feature, sort_order: index + 1 }))
    : null;
  if (directFeatures) {
    return {
      note_zh: text(source.note_zh) || text(source.body_zh),
      note_en: text(source.note_en) || text(source.body_en),
      note_zh_tw: text(source.note_zh_tw) || text(source.body_zh_tw),
      note_ja: text(source.note_ja) || text(source.body_ja),
      features: directFeatures
    };
  }
  const storedFeatures = structuredFeatures(source.highlights_zh, previewVersion);
  if (storedFeatures) {
    return {
      note_zh: text(source.body_zh),
      note_en: text(source.body_en),
      note_zh_tw: text(source.body_zh_tw),
      note_ja: text(source.body_ja),
      features: storedFeatures
    };
  }

  const previewId = text(source.id, 160) || "release-preview";
  const highlightZh = lines(source.highlights_zh);
  const highlightEn = lines(source.highlights_en);
  const highlightZhTw = lines(source.highlights_zh_tw);
  const highlightJa = lines(source.highlights_ja);

  if (highlightZh.length || highlightEn.length || highlightZhTw.length || highlightJa.length) {
    const length = Math.max(highlightZh.length, highlightEn.length, highlightZhTw.length, highlightJa.length);
    return {
      note_zh: text(source.body_zh),
      note_en: text(source.body_en),
      note_zh_tw: text(source.body_zh_tw),
      note_ja: text(source.body_ja),
      features: Array.from({ length }, (_, index) => featureFromLegacy(previewId, previewVersion, index, highlightZh, highlightEn, highlightZhTw, highlightJa))
    };
  }

  const bodyZh = splitPreviewItems(source.body_zh);
  const bodyEn = splitPreviewItems(source.body_en);
  const bodyZhTw = splitPreviewItems(source.body_zh_tw);
  const bodyJa = splitPreviewItems(source.body_ja);
  const knownNote = bodyZh[0] === KNOWN_V32_NOTE_ZH;
  const zh = knownNote ? bodyZh.slice(1) : bodyZh;
  const en = knownNote ? bodyEn.slice(1) : bodyEn;
  const zhTw = knownNote ? bodyZhTw.slice(1) : bodyZhTw;
  const ja = knownNote ? bodyJa.slice(1) : bodyJa;
  const length = Math.max(zh.length, en.length, zhTw.length, ja.length);
  return {
    note_zh: knownNote ? bodyZh[0] : "",
    note_en: knownNote ? (bodyEn[0] ?? "") : "",
    note_zh_tw: knownNote ? (bodyZhTw[0] ?? "") : "",
    note_ja: knownNote ? (bodyJa[0] ?? "") : "",
    features: Array.from({ length }, (_, index) => featureFromLegacy(previewId, previewVersion, index, zh, en, zhTw, ja))
  };
}

export function encodeReleasePreviewFeatures(features: ReleasePreviewFeature[]): StoredReleasePreviewEnvelope {
  const normalized = features.map((feature, index) => ({ ...feature, sort_order: index + 1 }));
  return [
    { schema_version: RELEASE_PREVIEW_SCHEMA_VERSION, features: normalized },
    ...normalized.map((feature) => feature.content_zh)
  ];
}

export function localizedFeatureContent(feature: ReleasePreviewFeature, locale: "zh" | "en" | "zhHant" | "ja") {
  if (locale === "zh") return feature.content_zh;
  if (locale === "zhHant") return feature.content_zh_tw || feature.content_zh;
  if (locale === "ja") return feature.content_ja || feature.content_en || feature.content_zh;
  return feature.content_en || feature.content_zh;
}

export function localizedFeatureTitle(feature: ReleasePreviewFeature, locale: "zh" | "en" | "zhHant" | "ja") {
  if (locale === "zh") return feature.title_zh;
  if (locale === "zhHant") return feature.title_zh_tw || feature.title_zh;
  if (locale === "ja") return feature.title_ja || feature.title_en || feature.title_zh;
  return feature.title_en || feature.title_zh;
}

export function localizedFeatureDescription(feature: ReleasePreviewFeature, locale: "zh" | "en" | "zhHant" | "ja") {
  if (locale === "zh") return feature.description_zh;
  if (locale === "zhHant") return feature.description_zh_tw || feature.description_zh;
  if (locale === "ja") return feature.description_ja || feature.description_en || feature.description_zh;
  return feature.description_en || feature.description_zh;
}

export function parseReleasePreviewBulkLine(value: string) {
  const line = value.trim();
  const separator = line.search(/[|｜]/);
  if (separator <= 0) return { title: "", description: line };
  const title = line.slice(0, separator).trim();
  const description = line.slice(separator + 1).trim();
  return title && description ? { title, description } : { title: "", description: line };
}

export function releasePreviewFeatureVersion(feature: ReleasePreviewFeature, previewVersion: string) {
  return feature.target_version.trim() || previewVersion.trim();
}

export function suggestedReleasePreviewVersion(version: string) {
  const normalized = version.trim();
  const match = normalized.match(/^(.*?)(\d+)(\D*)$/);
  if (!match) return "";
  return `${match[1]}${Number(match[2]) + 1}${match[3]}`;
}

export function localizedPreviewNote(content: NormalizedReleasePreviewContent, locale: "zh" | "en" | "zhHant" | "ja") {
  if (locale === "zh") return content.note_zh;
  if (locale === "zhHant") return content.note_zh_tw || content.note_zh;
  if (locale === "ja") return content.note_ja || content.note_en || content.note_zh;
  return content.note_en || content.note_zh;
}
