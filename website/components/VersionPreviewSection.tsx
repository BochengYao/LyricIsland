"use client";

import { useEffect, useState } from "react";
import { ReleasePreviewProgressRing } from "@/components/ReleasePreviewProgressRing";
import { Eyebrow } from "@/components/SitePage";
import { incentivesByLocale } from "@/data/incentives-copy";
import type { ReleasePreview } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";
import { preloadClientJson } from "@/lib/client-data";
import { formatReleaseTiming } from "@/lib/release-timing";
import { localizedFeatureContent, localizedPreviewNote, normalizeReleasePreviewContent } from "@/data/release-preview-content";

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
          const previewContent = normalizeReleasePreviewContent(preview);
          const note = localizedPreviewNote(previewContent, locale);
          const features = [...previewContent.features]
            .sort((left, right) => left.sort_order - right.sort_order)
            .map((feature) => ({ feature, content: localizedFeatureContent(feature, locale).trim() }))
            .filter(({ content }) => Boolean(content));
          return (
            <article className="previewCard" key={preview.id}>
              <div className="previewCardMeta">
                <strong>{preview.version}</strong>
                <small>{copy.target} {formatReleaseTiming(preview.target_date, locale)}</small>
                {note && <p className="previewNote">{note}</p>}
              </div>
              <div className="previewCardContent">
                <ul className="previewItems">
                  {features.map(({ feature, content }) => (
                    <li key={feature.id}>
                      <ReleasePreviewProgressRing progress={feature.progress} locale={locale} />
                      <p>{content}</p>
                    </li>
                  ))}
                </ul>
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
