import type { ReleasePreviewFeature } from "@/data/incentives-types";

export const RELEASE_PREVIEW_SCHEMA_VERSION = 2;
export const RELEASE_PREVIEW_PROGRESS_ANCHORS = [0, 10, 30, 50, 65, 80, 90, 95, 100] as const;
export const KNOWN_V32_NOTE_ZH = "新版本的主要功能已基本完成，目前正在进一步优化性能、功耗与长期运行体验，发布时间调整至本月内。";

export type StoredReleasePreviewFeatures = {
  schema_version: typeof RELEASE_PREVIEW_SCHEMA_VERSION;
  features: ReleasePreviewFeature[];
};

export type StoredReleasePreviewEnvelope = Array<StoredReleasePreviewFeatures | string>;

type PreviewContentSource = {
  id?: unknown;
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
  return RELEASE_PREVIEW_PROGRESS_ANCHORS.includes(rounded as typeof RELEASE_PREVIEW_PROGRESS_ANCHORS[number])
    ? rounded
    : 0;
}

function safeStage(value: unknown, progress: number): ReleasePreviewFeature["stage"] {
  return value === "ready" && progress === 100 ? "ready" : "development";
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

function sanitizeFeature(value: unknown, fallbackOrder: number): ReleasePreviewFeature | null {
  if (!value || typeof value !== "object") return null;
  const source = value as Record<string, unknown>;
  const id = text(source.id, 120);
  const contentZh = text(source.content_zh);
  const contentEn = text(source.content_en);
  const contentZhTw = text(source.content_zh_tw);
  const contentJa = text(source.content_ja);
  if (!id || (!contentZh && !contentEn && !contentZhTw && !contentJa)) return null;
  const rawOrder = typeof source.sort_order === "number" && Number.isFinite(source.sort_order)
    ? Math.round(source.sort_order)
    : fallbackOrder;
  const progress = safeProgress(source.progress);
  return {
    id,
    sort_order: Math.max(0, rawOrder),
    progress,
    stage: safeStage(source.stage, progress),
    content_zh: contentZh,
    content_en: contentEn,
    content_zh_tw: contentZhTw,
    content_ja: contentJa
  };
}

function structuredFeatures(value: unknown) {
  if (!value || typeof value !== "object") return null;
  const candidate = Array.isArray(value)
    ? value.find((item) => Boolean(item) && typeof item === "object" && !Array.isArray(item))
    : value;
  if (!candidate || typeof candidate !== "object" || Array.isArray(candidate)) return null;
  const source = candidate as Record<string, unknown>;
  if (source.schema_version !== RELEASE_PREVIEW_SCHEMA_VERSION || !Array.isArray(source.features)) return null;
  const usedIds = new Set<string>();
  return source.features
    .map((feature, index) => sanitizeFeature(feature, index + 1))
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
  index: number,
  zh: string[],
  en: string[],
  zhTw: string[],
  ja: string[]
): ReleasePreviewFeature {
  const primary = zh[index] || en[index] || zhTw[index] || ja[index] || `feature-${index + 1}`;
  return {
    id: stableLegacyFeatureId(previewId, primary, index),
    sort_order: index + 1,
    progress: 0,
    stage: "development",
    content_zh: zh[index] ?? "",
    content_en: en[index] ?? "",
    content_zh_tw: zhTw[index] ?? "",
    content_ja: ja[index] ?? ""
  };
}

export function normalizeReleasePreviewContent(source: PreviewContentSource): NormalizedReleasePreviewContent {
  const directFeatures = Array.isArray(source.features)
    ? source.features
      .map((feature, index) => sanitizeFeature(feature, index + 1))
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
  const storedFeatures = structuredFeatures(source.highlights_zh);
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
      features: Array.from({ length }, (_, index) => featureFromLegacy(previewId, index, highlightZh, highlightEn, highlightZhTw, highlightJa))
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
    features: Array.from({ length }, (_, index) => featureFromLegacy(previewId, index, zh, en, zhTw, ja))
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

export function localizedPreviewNote(content: NormalizedReleasePreviewContent, locale: "zh" | "en" | "zhHant" | "ja") {
  if (locale === "zh") return content.note_zh;
  if (locale === "zhHant") return content.note_zh_tw || content.note_zh;
  if (locale === "ja") return content.note_ja || content.note_en || content.note_zh;
  return content.note_en || content.note_zh;
}
