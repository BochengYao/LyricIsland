import { ReleasePreviewProgressRing } from "@/components/ReleasePreviewProgressRing";
import type { ReleasePreview, ReleasePreviewFeature } from "@/data/incentives-types";
import {
  localizedFeatureDescription,
  localizedFeatureTitle,
  localizedPreviewNote,
  normalizeReleasePreviewContent
} from "@/data/release-preview-content";
import type { Locale } from "@/data/site-copy";
import { formatReleaseTiming } from "@/lib/release-timing";

function groupCopy(locale: Locale) {
  if (locale === "zh") return { note: "开发说明", featured: "核心功能", improvement: "其他改进", next: "接下来" };
  if (locale === "zhHant") return { note: "開發說明", featured: "核心功能", improvement: "其他改進", next: "接下來" };
  if (locale === "ja") return { note: "開発ノート", featured: "主な機能", improvement: "その他の改善", next: "次に登場" };
  return { note: "Development Note", featured: "Featured", improvement: "Other improvements", next: "Coming next" };
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
  locale,
  targetLabel
}: {
  preview: ReleasePreview;
  locale: Locale;
  targetLabel: string;
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
  const featured = features.filter((item) => item.feature.display_group === "featured");
  const improvements = features.filter((item) => item.feature.display_group === "improvement");
  const futureGroups = new Map<string, LocalizedFeature[]>();
  features.filter((item) => item.feature.display_group === "future").forEach((item) => {
    const version = item.feature.target_version.trim() || preview.version;
    futureGroups.set(version, [...(futureGroups.get(version) ?? []), item]);
  });

  return (
    <article className="previewCard">
      <header className="previewCardMeta">
        <strong>{preview.version}</strong>
        <small>{targetLabel} {formatReleaseTiming(preview.target_date, locale)}</small>
      </header>
      {note && (
        <div className="previewNoteBlock">
          <span className="previewNoteLabel">{copy.note}</span>
          <p className="previewNote">{note}</p>
        </div>
      )}
      {featured.length > 0 && (
        <section className="previewFeatureGroup previewFeaturedGroup">
          <h3>{copy.featured}</h3>
          <ul className="previewItems previewFeaturedList">
            {featured.map((item) => <FeatureRow item={item} locale={locale} key={item.feature.id} />)}
          </ul>
        </section>
      )}
      {improvements.length > 0 && (
        <section className="previewFeatureGroup previewImprovementGroup">
          <h3>{copy.improvement}</h3>
          <ul className="previewItems previewImprovementList">
            {improvements.map((item) => <FeatureRow item={item} locale={locale} key={item.feature.id} />)}
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
