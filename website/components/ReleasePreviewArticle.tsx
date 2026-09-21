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
  if (locale === "zh") return { note: "开发说明", current: "新功能与改进", next: "接下来" };
  if (locale === "zhHant") return { note: "開發說明", current: "新功能與改進", next: "接下來" };
  if (locale === "ja") return { note: "開発ノート", current: "新機能と改善", next: "次に登場" };
  return { note: "Development Note", current: "Features and improvements", next: "Coming next" };
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
  const currentFeatures = features.filter((item) => item.feature.display_group !== "future");
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
