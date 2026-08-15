"use client";

import { useEffect, useState } from "react";
import { Eyebrow } from "@/components/SitePage";
import { incentivesByLocale } from "@/data/incentives-copy";
import type { ReleasePreview } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";
import { preloadClientJson } from "@/lib/client-data";

function splitPreviewItems(value: string): string[] {
  return value
    .split(/\r?\n|[；;]/)
    .map((item) => item.replace(/^\s*(?:[-*•]|\d+[.)、])\s*/, "").trim())
    .filter(Boolean);
}

function comparePreviewVersions(left: ReleasePreview, right: ReleasePreview) {
  const leftParts = left.version.match(/\d+/g)?.map(Number) ?? [];
  const rightParts = right.version.match(/\d+/g)?.map(Number) ?? [];
  const length = Math.max(leftParts.length, rightParts.length);
  for (let index = 0; index < length; index += 1) {
    const difference = (leftParts[index] ?? 0) - (rightParts[index] ?? 0);
    if (difference) return difference;
  }
  return left.version.localeCompare(right.version, undefined, { numeric: true, sensitivity: "base" });
}

type PublicIncentivesResponse = { previews?: ReleasePreview[] };

const publicIncentivesPreload = preloadClientJson<PublicIncentivesResponse>("/api/incentives/public");

function pickText(...values: string[]) {
  return values.find((value) => value?.trim()) ?? "";
}

function pickItems(...values: string[][]) {
  return values.find((value) => value?.length) ?? [];
}

function localizedPreview(preview: ReleasePreview, locale: Locale) {
  if (locale === "zh") {
    return {
      title: preview.title_zh,
      body: preview.body_zh,
      highlights: preview.highlights_zh
    };
  }

  if (locale === "zhHant") {
    return {
      title: pickText(preview.title_zh_tw, preview.title_zh),
      body: pickText(preview.body_zh_tw, preview.body_zh),
      highlights: pickItems(preview.highlights_zh_tw, preview.highlights_zh)
    };
  }

  if (locale === "ja") {
    return {
      title: pickText(preview.title_ja, preview.title_en, preview.title_zh),
      body: pickText(preview.body_ja, preview.body_en, preview.body_zh),
      highlights: pickItems(preview.highlights_ja, preview.highlights_en, preview.highlights_zh)
    };
  }

  return {
    title: pickText(preview.title_en, preview.title_zh),
    body: pickText(preview.body_en, preview.body_zh),
    highlights: pickItems(preview.highlights_en, preview.highlights_zh)
  };
}

function previewStateCopy(locale: Locale) {
  if (locale === "zh") return { loading: "正在载入", failed: "版本预告暂时无法载入，请稍后刷新。", tbd: "待定" };
  if (locale === "zhHant") return { loading: "正在載入", failed: "版本預告暫時無法載入，請稍後重新整理。", tbd: "待定" };
  if (locale === "ja") return { loading: "読み込んでいます", failed: "リリース予定を読み込めません。しばらくしてから再読み込みしてください。", tbd: "未定" };
  return { loading: "Loading", failed: "Release previews could not be loaded. Please refresh later.", tbd: "TBD" };
}

export function VersionPreviewSection({ locale }: { locale: Locale }) {
  const copy = incentivesByLocale[locale].preview;
  const stateCopy = previewStateCopy(locale);
  const [previews, setPreviews] = useState<ReleasePreview[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let active = true;
    const request = publicIncentivesPreload ?? preloadClientJson<PublicIncentivesResponse>("/api/incentives/public");
    request
      ?.then((data) => {
        if (active) setPreviews(data.previews ?? []);
      })
      .catch(() => {
        if (active) setLoadFailed(true);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <section
      className="previewSection updatesPreviewSection sectionContainer"
      id="release-preview"
      aria-busy={loading}
      data-snap-section
    >
      <div className="previewIntro">
        <Eyebrow reveal>{copy.eyebrow}</Eyebrow>
        <h2 data-text-reveal="title">{copy.title}</h2>
        <p>{copy.body}</p>
      </div>
      <div className={`previewList${loading ? "" : " databaseContentReveal"}`} aria-live="polite">
        {previews.length ? [...previews].sort(comparePreviewVersions).map((preview) => {
          const { title, body, highlights } = localizedPreview(preview, locale);
          const items = [...splitPreviewItems(body), ...highlights.map((item) => item.trim()).filter(Boolean)];
          return (
            <article className="previewCard" key={preview.id}>
              <div className="previewCardMeta">
                <strong>{preview.version}</strong>
                <small>{copy.target} {preview.target_date ?? stateCopy.tbd}</small>
              </div>
              <div className="previewCardContent">
                {title !== preview.version && <h3>{title}</h3>}
                <ol className="previewItems">
                  {items.map((item, itemIndex) => (
                    <li key={`${itemIndex}-${item}`}>
                      <span className="previewItemNumber" aria-hidden="true">{String(itemIndex + 1).padStart(2, "0")}</span>
                      <p>{item}</p>
                    </li>
                  ))}
                </ol>
              </div>
            </article>
          );
        }) : <p className="previewEmpty">{loading
          ? stateCopy.loading
          : loadFailed
            ? stateCopy.failed
            : copy.empty}</p>}
      </div>
    </section>
  );
}
