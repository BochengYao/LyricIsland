import defaultPreviewJson from "@/data/release-preview-default.json";
import type { ReleasePreview } from "@/data/incentives-types";

export type ReleasePreviewInput = Omit<
  ReleasePreview,
  "id" | "created_at" | "updated_at" | "published_at"
>;

export const defaultReleasePreview = defaultPreviewJson as ReleasePreviewInput;

export function releasePreviewFallback(): ReleasePreview {
  const timestamp = "2026-07-29T00:00:00.000Z";
  return {
    id: "default-release-preview-v2-1",
    ...defaultReleasePreview,
    created_at: timestamp,
    updated_at: timestamp,
    published_at: timestamp
  };
}
