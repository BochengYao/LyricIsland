import defaultPreviewJson from "@/data/release-preview-default.json";
export type ReleasePreviewInput = {
  version: string;
  title_zh: string;
  title_en: string;
  title_zh_tw?: string;
  title_ja?: string;
  body_zh: string;
  body_en: string;
  body_zh_tw?: string;
  body_ja?: string;
  highlights_zh: unknown;
  highlights_en: string[];
  highlights_zh_tw?: string[];
  highlights_ja?: string[];
  target_date: string | null;
  status: "draft" | "published";
};

export const defaultReleasePreview = defaultPreviewJson as ReleasePreviewInput;

export function releasePreviewFallback() {
  const timestamp = "2026-07-29T00:00:00.000Z";
  return {
    id: "default-release-preview-v2-1",
    ...defaultReleasePreview,
    title_zh_tw: defaultReleasePreview.title_zh_tw || defaultReleasePreview.title_zh,
    title_ja: defaultReleasePreview.title_ja || defaultReleasePreview.title_en || defaultReleasePreview.title_zh,
    body_zh_tw: defaultReleasePreview.body_zh_tw || defaultReleasePreview.body_zh,
    body_ja: defaultReleasePreview.body_ja || defaultReleasePreview.body_en || defaultReleasePreview.body_zh,
    highlights_zh_tw: defaultReleasePreview.highlights_zh_tw?.length
      ? defaultReleasePreview.highlights_zh_tw
      : defaultReleasePreview.highlights_zh,
    highlights_ja: defaultReleasePreview.highlights_ja?.length
      ? defaultReleasePreview.highlights_ja
      : (defaultReleasePreview.highlights_en.length
        ? defaultReleasePreview.highlights_en
        : defaultReleasePreview.highlights_zh),
    created_at: timestamp,
    updated_at: timestamp,
    published_at: timestamp
  };
}
