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

type PublicIncentivesResponse = { previews?: ReleasePreview[] };

const publicIncentivesPreload = preloadClientJson<PublicIncentivesResponse>("/api/incentives/public");

export function VersionPreviewSection({ locale }: { locale: Locale }) {
  const copy = incentivesByLocale[locale].preview;
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
        {previews.length ? previews.map((preview) => {
          const title = locale === "zh" ? preview.title_zh : preview.title_en || preview.title_zh;
          const body = locale === "zh" ? preview.body_zh : preview.body_en || preview.body_zh;
          const highlights = locale === "zh"
            ? preview.highlights_zh
            : preview.highlights_en.length
              ? preview.highlights_en
              : preview.highlights_zh;
          const items = [...splitPreviewItems(body), ...highlights.map((item) => item.trim()).filter(Boolean)];
          return (
            <article className="previewCard" key={preview.id}>
              <div className="previewCardMeta">
                <small>{preview.version} · {copy.target} {preview.target_date ?? (locale === "zh" ? "待定" : "TBD")}</small>
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
          ? (locale === "zh" ? "正在载入" : "Loading")
          : loadFailed
            ? (locale === "zh" ? "版本预告暂时无法载入，请稍后刷新。" : "Release previews could not be loaded. Please refresh later.")
            : copy.empty}</p>}
      </div>
    </section>
  );
}
