import defaultPreviewJson from "@/data/release-preview-default.json";
import type { ReleasePreview } from "@/data/incentives-types";

export type ReleasePreviewInput = Omit<
  ReleasePreview,
  | "id"
  | "created_at"
  | "updated_at"
  | "published_at"
  | "title_zh_tw"
  | "title_ja"
  | "body_zh_tw"
  | "body_ja"
  | "highlights_zh_tw"
  | "highlights_ja"
> & Partial<Pick<
  ReleasePreview,
  | "title_zh_tw"
  | "title_ja"
  | "body_zh_tw"
  | "body_ja"
  | "highlights_zh_tw"
  | "highlights_ja"
>>;

export const defaultReleasePreview = defaultPreviewJson as ReleasePreviewInput;

export function releasePreviewFallback(): ReleasePreview {
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
