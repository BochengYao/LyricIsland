import defaultContentJson from "@/data/feature-content-default.json";
import type { FeatureContent, FeatureContentSection } from "@/data/incentives-types";

export const defaultFeatureContent = defaultContentJson as FeatureContent;

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
      return {
        id: cleanText(item.id, 80) || `feature-${String(index + 1).padStart(2, "0")}`,
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

  return {
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

export function localizedFeatureContent(content: FeatureContent, locale: "zh" | "en") {
  const suffix = locale === "zh" ? "zh" : "en";
  const localizeText = locale === "en" ? normalizeEnglishPlayerNames : (value: string) => value;
  return {
    summaryLabel: localizeText(content.summary[`label_${suffix}`]),
    summary: content.summary[`items_${suffix}`].map(localizeText),
    summaryVisible: content.summary.visible,
    sections: content.sections
      .filter((section) => section.visible)
      .map((section, index) => ({
        number: String(index + 1).padStart(2, "0"),
        title: localizeText(section[`title_${suffix}`]),
        body: localizeText(section[`body_${suffix}`]),
        items: section[`items_${suffix}`].map(localizeText)
      }))
  };
}
