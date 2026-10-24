import defaultContentJson from "@/data/feature-content-default.json";
import type { AppUpdatePrompt, FeatureContent, FeatureContentSection } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";

export const defaultFeatureContent = defaultContentJson as FeatureContent;

export const LEGACY_FEATURE_RELEASE_VERSION = "早期更新";
const FEATURE_RELEASE_VERSION_PATTERN = /^v\d+\.\d+\.\d+$/i;

export type ExtractedFeatureText = {
  title: string;
  description: string;
  items: string[];
};

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

function cleanFeatureExtractLine(value: string) {
  return value
    .replace(/^\s*#{1,6}\s*/, "")
    .replace(/\*\*/g, "")
    .trim();
}

function isFeatureExtractDivider(value: string) {
  return /^\s*(?:-{3,}|={3,}|_{3,}|\*{3,})\s*$/.test(value);
}

function isFeatureExtractTitleLabel(value: string) {
  return /^(?:标题|功能标题|title)\s*[:：]/i.test(cleanFeatureExtractLine(value));
}

function featureExtractBullet(value: string) {
  const match = value.match(/^\s*(?:[-–—*•·▪◦]|\d+[.)、])\s*(.+)$/);
  return match?.[1].trim() ?? "";
}

function splitFeatureExtractItems(value: string) {
  return value
    .split(/[；;]/)
    .map((item) => featureExtractBullet(item) || item.trim())
    .filter(Boolean);
}

export function parseFeatureContentText(value: unknown): ExtractedFeatureText {
  if (typeof value !== "string") return { title: "", description: "", items: [] };
  const lines = value.replace(/\r\n?/g, "\n").split("\n");
  const title: string[] = [];
  const description: string[] = [];
  const items: string[] = [];
  let field: "title" | "description" | "items" | null = null;
  let recognizedLabel = false;

  for (const rawLine of lines) {
    if (isFeatureExtractDivider(rawLine)) continue;
    const line = cleanFeatureExtractLine(rawLine);
    if (!line) continue;

    const label = line.match(/^(标题|功能标题|title|描述|功能描述|说明|description|分条|要点|功能点|条目|bullets?|items?)\s*[:：]\s*(.*)$/i);
    if (label) {
      recognizedLabel = true;
      const name = label[1].toLocaleLowerCase();
      const content = label[2].trim();
      field = /标题|title/.test(name)
        ? "title"
        : /描述|说明|description/.test(name)
          ? "description"
          : "items";
      if (!content) continue;
      if (field === "items") items.push(...splitFeatureExtractItems(content));
      else if (field === "title") title.push(content);
      else description.push(content);
      continue;
    }

    const bullet = featureExtractBullet(line);
    if (bullet) {
      items.push(bullet);
      field = "items";
      continue;
    }

    if (field === "items") items.push(...splitFeatureExtractItems(line));
    else if (field === "title") title.push(line);
    else if (field === "description") description.push(line);
    else if (!recognizedLabel && title.length === 0) {
      const separator = line.search(/[|｜]/);
      if (separator > 0) {
        title.push(line.slice(0, separator).trim());
        const compactDescription = line.slice(separator + 1).trim();
        if (compactDescription) description.push(compactDescription);
      } else {
        title.push(line);
      }
    } else {
      description.push(line);
    }
  }

  return {
    title: title.join(" ").trim().slice(0, 160),
    description: description.join("\n").trim().slice(0, 1200),
    items: items.map((item) => item.slice(0, 240)).filter(Boolean).slice(0, 12)
  };
}

export function parseFeatureContentModules(value: unknown): ExtractedFeatureText[] {
  if (typeof value !== "string" || !value.trim()) return [];
  const normalized = value.replace(/\r\n?/g, "\n").trim();
  const lines = normalized.split("\n");
  const titleLabelCount = lines.filter(isFeatureExtractTitleLabel).length;
  const hasExplicitDivider = lines.some(isFeatureExtractDivider);
  let blocks: string[];

  if (hasExplicitDivider) {
    blocks = normalized
      .split(/^\s*(?:-{3,}|={3,}|_{3,}|\*{3,})\s*$/m)
      .map((block) => block.trim())
      .filter(Boolean);
  } else if (titleLabelCount > 0) {
    const collected: string[][] = [];
    let current: string[] = [];
    for (const line of lines) {
      if (isFeatureExtractTitleLabel(line)) {
        if (current.some((item) => item.trim())) collected.push(current);
        current = [line];
      } else if (current.length) {
        current.push(line);
      }
    }
    if (current.some((item) => item.trim())) collected.push(current);
    blocks = collected.map((block) => block.join("\n"));
  } else {
    blocks = normalized.split(/\n\s*\n+/).map((block) => block.trim()).filter(Boolean);
  }

  return blocks
    .map(parseFeatureContentText)
    .filter((module) => module.title || module.description || module.items.length);
}

export function sanitizeAppUpdatePrompt(value: unknown): AppUpdatePrompt {
  const source = value && typeof value === "object" ? value as Record<string, unknown> : {};
  return {
    enabled: source.enabled === true,
    release_version: cleanText(source.release_version, 40),
    content_zh: cleanText(source.content_zh, 4000),
    content_en: cleanText(source.content_en, 4000),
    content_zh_tw: cleanText(source.content_zh_tw, 4000),
    content_ja: cleanText(source.content_ja, 4000)
  };
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
    update_prompt: sanitizeAppUpdatePrompt(source.update_prompt),
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

const publicLyricsDisclosure = {
  zh: "在线歌词由第三方来源按需获取。",
  zhTw: "在線歌詞由第三方來源按需獲取。",
  en: "Online lyrics are fetched on demand from third-party sources.",
  ja: "オンライン歌詞は、第三者の提供元から必要に応じて取得します。"
};

function redactLyricSourceDetails(items: string[], pattern: RegExp, disclosure: string) {
  const retained = items.filter((item) => !pattern.test(item) && item !== disclosure);
  return retained.length === items.length ? items : [disclosure, ...retained];
}

function redactLyricSourceText(value: string, pattern: RegExp, disclosure: string) {
  return value === disclosure || !pattern.test(value) ? value : disclosure;
}

const lyricSourcePatterns = {
  zh: /歌词来源|歌词源|LRCLIB/i,
  zhTw: /歌[词詞](?:来源|來源|源)|LRCLIB/i,
  en: /\blyric (?:source|provider)s?\b|\bLRCLIB\b/i,
  ja: /歌詞(?:ソース|提供元)|\blyric (?:source|provider)s?\b|\bLRCLIB\b/i
};

export function publicFeatureContent(content: FeatureContent): FeatureContent {
  return {
    ...content,
    summary: {
      ...content.summary,
      label_zh: redactLyricSourceText(content.summary.label_zh, lyricSourcePatterns.zh, publicLyricsDisclosure.zh),
      label_zh_tw: redactLyricSourceText(content.summary.label_zh_tw, lyricSourcePatterns.zhTw, publicLyricsDisclosure.zhTw),
      label_en: redactLyricSourceText(content.summary.label_en, lyricSourcePatterns.en, publicLyricsDisclosure.en),
      label_ja: redactLyricSourceText(content.summary.label_ja, lyricSourcePatterns.ja, publicLyricsDisclosure.ja),
      items_zh: redactLyricSourceDetails(content.summary.items_zh, lyricSourcePatterns.zh, publicLyricsDisclosure.zh),
      items_zh_tw: redactLyricSourceDetails(content.summary.items_zh_tw, lyricSourcePatterns.zhTw, publicLyricsDisclosure.zhTw),
      items_en: redactLyricSourceDetails(content.summary.items_en, lyricSourcePatterns.en, publicLyricsDisclosure.en),
      items_ja: redactLyricSourceDetails(content.summary.items_ja, lyricSourcePatterns.ja, publicLyricsDisclosure.ja)
    },
    sections: content.sections.map((section) => ({
      ...section,
      title_zh: redactLyricSourceText(section.title_zh, lyricSourcePatterns.zh, publicLyricsDisclosure.zh),
      title_zh_tw: redactLyricSourceText(section.title_zh_tw, lyricSourcePatterns.zhTw, publicLyricsDisclosure.zhTw),
      title_en: redactLyricSourceText(section.title_en, lyricSourcePatterns.en, publicLyricsDisclosure.en),
      title_ja: redactLyricSourceText(section.title_ja, lyricSourcePatterns.ja, publicLyricsDisclosure.ja),
      body_zh: redactLyricSourceText(section.body_zh, lyricSourcePatterns.zh, publicLyricsDisclosure.zh),
      body_zh_tw: redactLyricSourceText(section.body_zh_tw, lyricSourcePatterns.zhTw, publicLyricsDisclosure.zhTw),
      body_en: redactLyricSourceText(section.body_en, lyricSourcePatterns.en, publicLyricsDisclosure.en),
      body_ja: redactLyricSourceText(section.body_ja, lyricSourcePatterns.ja, publicLyricsDisclosure.ja),
      items_zh: redactLyricSourceDetails(section.items_zh, lyricSourcePatterns.zh, publicLyricsDisclosure.zh),
      items_zh_tw: redactLyricSourceDetails(section.items_zh_tw, lyricSourcePatterns.zhTw, publicLyricsDisclosure.zhTw),
      items_en: redactLyricSourceDetails(section.items_en, lyricSourcePatterns.en, publicLyricsDisclosure.en),
      items_ja: redactLyricSourceDetails(section.items_ja, lyricSourcePatterns.ja, publicLyricsDisclosure.ja)
    }))
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
