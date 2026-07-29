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
  const sections = sectionsSource
    .filter((item): item is Record<string, unknown> => Boolean(item && typeof item === "object"))
    .slice(0, 30)
    .map((item, index): FeatureContentSection => ({
      id: cleanText(item.id, 80) || `feature-${String(index + 1).padStart(2, "0")}`,
      title_zh: cleanText(item.title_zh, 160),
      title_en: cleanText(item.title_en, 160),
      body_zh: cleanText(item.body_zh, 1200),
      body_en: cleanText(item.body_en, 1200),
      items_zh: cleanLines(item.items_zh, 12, 240),
      items_en: cleanLines(item.items_en, 12, 240),
      visible: item.visible !== false
    }))
    .filter((item) => item.title_zh || item.title_en);

  return {
    summary: {
      label_zh: cleanText(summarySource.label_zh, 80) || defaultFeatureContent.summary.label_zh,
      label_en: cleanText(summarySource.label_en, 80) || defaultFeatureContent.summary.label_en,
      items_zh: cleanLines(summarySource.items_zh, 12, 200),
      items_en: cleanLines(summarySource.items_en, 12, 200),
      visible: summarySource.visible !== false
    },
    sections
  };
}

export function localizedFeatureContent(content: FeatureContent, locale: "zh" | "en") {
  const suffix = locale === "zh" ? "zh" : "en";
  return {
    summaryLabel: content.summary[`label_${suffix}`],
    summary: content.summary[`items_${suffix}`],
    summaryVisible: content.summary.visible,
    sections: content.sections
      .filter((section) => section.visible)
      .map((section, index) => ({
        number: String(index + 1).padStart(2, "0"),
        title: section[`title_${suffix}`],
        body: section[`body_${suffix}`],
        items: section[`items_${suffix}`]
      }))
  };
}
