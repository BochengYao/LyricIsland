import { ReleasePreviewProgressRing } from "@/components/ReleasePreviewProgressRing";
import type { ReleasePreview, ReleasePreviewFeature } from "@/data/incentives-types";
import {
  localizedFeatureDescription,
  localizedFeatureTitle,
  localizedPreviewNote,
  normalizeReleasePreviewContent,
  releasePreviewFeatureVersion
} from "@/data/release-preview-content";
import type { Locale } from "@/data/site-copy";
import { formatReleaseTiming } from "@/lib/release-timing";

function groupCopy(locale: Locale) {
  if (locale === "zh") return { current: "新功能与改进", next: "接下来" };
  if (locale === "zhHant") return { current: "新功能與改進", next: "接下來" };
  if (locale === "ja") return { current: "新機能と改善", next: "次に登場" };
  return { current: "Features and improvements", next: "Coming next" };
}

function releaseTimingText(targetDate: string | null, locale: Locale) {
  const timing = formatReleaseTiming(targetDate, locale);
  if (locale === "zh") {
    if (timing === "待定") return "推出时间待定";
    return `预计${timing === "本周内" ? "本周" : timing === "本月内" ? "本月" : timing}推出`;
  }
  if (locale === "zhHant") {
    if (timing === "待定") return "推出時間待定";
    return `預計${timing === "本週內" ? "本週" : timing === "本月內" ? "本月" : timing}推出`;
  }
  if (locale === "ja") return timing === "未定" ? "リリース時期未定" : `${timing}にリリース予定`;
  return timing === "TBD" ? "Release timing TBD" : `Expected ${timing.toLocaleLowerCase("en")}`;
}

type LocalizedFeature = {
  feature: ReleasePreviewFeature;
  title: string;
  description: string;
};

function FeatureRow({ item, locale }: { item: LocalizedFeature; locale: Locale }) {
  const { feature, title, description } = item;
  return (
    <li className={title ? "" : "isLegacy"}>
      <div className="previewFeatureHeading">
        {title ? <h4>{title}</h4> : <p className="previewLegacyContent">{description}</p>}
        <ReleasePreviewProgressRing progress={feature.progress} stage={feature.stage} locale={locale} />
      </div>
      {title && <p>{description}</p>}
    </li>
  );
}

export function ReleasePreviewArticle({
  preview,
  locale
}: {
  preview: ReleasePreview;
  locale: Locale;
}) {
  const copy = groupCopy(locale);
  const content = normalizeReleasePreviewContent(preview);
  const note = localizedPreviewNote(content, locale);
  const features = [...content.features]
    .sort((left, right) => left.sort_order - right.sort_order)
    .map((feature) => ({
      feature,
      title: localizedFeatureTitle(feature, locale).trim(),
      description: localizedFeatureDescription(feature, locale).trim()
    }))
    .filter((item) => Boolean(item.description));
  const currentVersion = preview.version.trim();
  const currentFeatures = features.filter((item) => releasePreviewFeatureVersion(item.feature, currentVersion).toLocaleLowerCase() === currentVersion.toLocaleLowerCase());
  const futureGroups = new Map<string, LocalizedFeature[]>();
  features.filter((item) => releasePreviewFeatureVersion(item.feature, currentVersion).toLocaleLowerCase() !== currentVersion.toLocaleLowerCase()).forEach((item) => {
    const version = releasePreviewFeatureVersion(item.feature, currentVersion);
    futureGroups.set(version, [...(futureGroups.get(version) ?? []), item]);
  });

  return (
    <article className="previewCard">
      <header className="previewCardMeta">
        <strong>{preview.version}</strong>
        <small>{releaseTimingText(preview.target_date, locale)}</small>
      </header>
      {note && (
        <div className="previewNoteBlock">
          <p className="previewNote">{note}</p>
        </div>
      )}
      {currentFeatures.length > 0 && (
        <section className="previewFeatureGroup previewCurrentGroup">
          <h3>{copy.current}</h3>
          <ul className="previewItems previewCurrentList">
            {currentFeatures.map((item) => <FeatureRow item={item} locale={locale} key={item.feature.id} />)}
          </ul>
        </section>
      )}
      {[...futureGroups.entries()]
        .sort(([left], [right]) => left.localeCompare(right, undefined, { numeric: true, sensitivity: "base" }))
        .map(([version, items]) => (
        <section className="previewFeatureGroup previewFutureGroup" key={version}>
          <h3>{copy.next} · {version}</h3>
          <ul className="previewFutureList">
            {items.map(({ feature, title, description }) => (
              <li key={feature.id}>
                {title && <h4>{title}</h4>}
                <p>{description}</p>
              </li>
            ))}
          </ul>
        </section>
        ))}
    </article>
  );
}
