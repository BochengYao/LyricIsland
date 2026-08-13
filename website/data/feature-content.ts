import defaultContentJson from "@/data/feature-content-default.json";
import type { FeatureContent, FeatureContentSection } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";

export const defaultFeatureContent = defaultContentJson as FeatureContent;

export const LEGACY_FEATURE_RELEASE_VERSION = "早期更新";
const FEATURE_RELEASE_VERSION_PATTERN = /^v\d+\.\d+\.\d+$/i;
const PREVIEW_RELEASE_VERSION_PATTERN = /^v?\s*(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\s|$)/i;

/** Convert a preview label such as v3 or v2.5 to the persisted full version. */
export function featureReleaseVersionFromPreview(value: unknown) {
  const normalized = typeof value === "string" ? value.trim() : "";
  const match = normalized.match(PREVIEW_RELEASE_VERSION_PATTERN);
  if (!match) return null;
  return `v${match[1]}.${match[2] ?? "0"}.${match[3] ?? "0"}`;
}

export function isFeatureReleaseVersion(value: unknown) {
  const normalized = typeof value === "string" ? value.trim() : "";
  return normalized === LEGACY_FEATURE_RELEASE_VERSION || FEATURE_RELEASE_VERSION_PATTERN.test(normalized);
}

function majorVersionOf(version: string) {
  const match = version.trim().match(/^v?\s*(\d+)/i);
  return match ? `V${match[1]}` : "OTHER";
}

function cleanText(value: unknown, max: number) {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

function cleanLines(value: unknown, maxItems: number, maxLength: number) {
  return Array.isArray(value)
    ? value
        .filter((item): item is string => typeof item === "string")
        .map((item) => item.trim().slice(0, maxLength))
        .filter(Boolean)
        .slice(0, maxItems)
    : [];
}

export function sanitizeFeatureContent(value: unknown): FeatureContent {
  const source = value && typeof value === "object"
    ? value as Record<string, unknown>
    : {};
  const summarySource = source.summary && typeof source.summary === "object"
    ? source.summary as Record<string, unknown>
    : {};
  const sectionsSource = Array.isArray(source.sections) ? source.sections : [];
  const summaryLabelZh = cleanText(summarySource.label_zh, 80) || defaultFeatureContent.summary.label_zh;
  const summaryLabelEn = cleanText(summarySource.label_en, 80) || defaultFeatureContent.summary.label_en;
  const summaryItemsZh = cleanLines(summarySource.items_zh, 12, 200);
  const summaryItemsEn = cleanLines(summarySource.items_en, 12, 200);
  const summaryItemsZhTw = cleanLines(summarySource.items_zh_tw, 12, 200);
  const summaryItemsJa = cleanLines(summarySource.items_ja, 12, 200);
  const sections = sectionsSource
    .filter((item): item is Record<string, unknown> => Boolean(item && typeof item === "object"))
    .slice(0, 30)
    .map((item, index): FeatureContentSection => {
      const titleZh = cleanText(item.title_zh, 160);
      const titleEn = cleanText(item.title_en, 160);
      const bodyZh = cleanText(item.body_zh, 1200);
      const bodyEn = cleanText(item.body_en, 1200);
      const itemsZh = cleanLines(item.items_zh, 12, 240);
      const itemsEn = cleanLines(item.items_en, 12, 240);
      const itemsZhTw = cleanLines(item.items_zh_tw, 12, 240);
      const itemsJa = cleanLines(item.items_ja, 12, 240);
      const releaseVersion = cleanText(item.release_version, 40) || LEGACY_FEATURE_RELEASE_VERSION;
      return {
        id: cleanText(item.id, 80) || `feature-${String(index + 1).padStart(2, "0")}`,
        release_version: releaseVersion,
        major_version: majorVersionOf(releaseVersion),
        title_zh: titleZh,
        title_en: titleEn,
        title_zh_tw: cleanText(item.title_zh_tw, 160) || titleZh,
        title_ja: cleanText(item.title_ja, 160) || titleEn || titleZh,
        body_zh: bodyZh,
        body_en: bodyEn,
        body_zh_tw: cleanText(item.body_zh_tw, 1200) || bodyZh,
        body_ja: cleanText(item.body_ja, 1200) || bodyEn || bodyZh,
        items_zh: itemsZh,
        items_en: itemsEn,
        items_zh_tw: itemsZhTw.length ? itemsZhTw : itemsZh,
        items_ja: itemsJa.length ? itemsJa : (itemsEn.length ? itemsEn : itemsZh),
        visible: item.visible !== false
      };
    })
    .filter((item) => item.title_zh || item.title_en);
  const sourceVersions = Array.isArray(source.versions) ? source.versions : [];
  const versions = [...sourceVersions, ...sections.map((section) => section.release_version)]
    .map((value) => cleanText(value, 40))
    .filter((value): value is string => Boolean(value) && value !== LEGACY_FEATURE_RELEASE_VERSION && FEATURE_RELEASE_VERSION_PATTERN.test(value));

  return {
    versions: [...new Set(versions)],
    summary: {
      label_zh: summaryLabelZh,
      label_en: summaryLabelEn,
      label_zh_tw: cleanText(summarySource.label_zh_tw, 80) || summaryLabelZh,
      label_ja: cleanText(summarySource.label_ja, 80) || summaryLabelEn || summaryLabelZh,
      items_zh: summaryItemsZh,
      items_en: summaryItemsEn,
      items_zh_tw: summaryItemsZhTw.length ? summaryItemsZhTw : summaryItemsZh,
      items_ja: summaryItemsJa.length ? summaryItemsJa : (summaryItemsEn.length ? summaryItemsEn : summaryItemsZh),
      visible: summarySource.visible !== false
    },
    sections
  };
}

function normalizeEnglishPlayerNames(value: string) {
  return value
    .replace(/\bKugou(?:\s+Music)?\b/gi, "Kugou Music")
    .replace(/\bKuwo(?:\s+Music)?\b/gi, "Kuwo Music");
}

export function localizedFeatureContent(content: FeatureContent, locale: Locale) {
  const localizeText = locale === "en" ? normalizeEnglishPlayerNames : (value: string) => value;
  const pick = (zh: string, zhTw: string, en: string, ja: string) => locale === "zh"
    ? zh
    : locale === "zhHant"
      ? (zhTw || zh)
      : locale === "ja"
        ? (ja || en || zh)
        : (en || zh);
  const pickItems = (zh: string[], zhTw: string[], en: string[], ja: string[]) => locale === "zh"
    ? zh
    : locale === "zhHant"
      ? (zhTw.length ? zhTw : zh)
      : locale === "ja"
        ? (ja.length ? ja : en.length ? en : zh)
        : (en.length ? en : zh);

  return {
    summaryLabel: localizeText(pick(content.summary.label_zh, content.summary.label_zh_tw, content.summary.label_en, content.summary.label_ja)),
    summary: pickItems(content.summary.items_zh, content.summary.items_zh_tw, content.summary.items_en, content.summary.items_ja).map(localizeText),
    summaryVisible: content.summary.visible,
    sections: content.sections
      .filter((section) => section.visible)
      .map((section, index) => ({
        number: String(index + 1).padStart(2, "0"),
        title: localizeText(pick(section.title_zh, section.title_zh_tw, section.title_en, section.title_ja)),
        body: localizeText(pick(section.body_zh, section.body_zh_tw, section.body_en, section.body_ja)),
        items: pickItems(section.items_zh, section.items_zh_tw, section.items_en, section.items_ja).map(localizeText)
      }))
  };
}
