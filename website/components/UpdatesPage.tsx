import Link from "next/link";
import { DatabasePreload } from "@/components/DatabasePreload";
import { ExternalArrow } from "@/components/ExternalArrow";
import { ManagedFeatureContent } from "@/components/ManagedFeatureContent";
import { Eyebrow, LogoLockup, PrimaryNavigation } from "@/components/SitePage";
import { SelectiveTextReveal } from "@/components/SelectiveTextReveal";
import { SmoothSectionScroll } from "@/components/SmoothSectionScroll";
import {
  copyByLocale,
  displayBrand,
  localePath,
  microsoftStoreUrl,
  type Locale
} from "@/data/site-copy";
import { updatesByLocale } from "@/data/updates-copy";

type Props = {
  locale: Locale;
};

const githubUrl = "https://github.com/BochengYao/LyricHover";

export function UpdatesPage({ locale }: Props) {
  const siteCopy = copyByLocale[locale];
  const copy = updatesByLocale[locale];
  const home = localePath(locale, "home");

  return (
    <>
      <DatabasePreload href="/api/features" />
      <a className="skipLink" href="#updates-main">
        {locale === "zh" ? "跳到更新内容" : locale === "zhHant" ? "跳至更新內容" : locale === "ja" ? "更新内容へ移動" : "Skip to updates"}
      </a>
      <SmoothSectionScroll />
      <SelectiveTextReveal />
      <PrimaryNavigation locale={locale} homeHref={home} page="updates" />

      <main id="updates-main" className="updatesMain">
        <ManagedFeatureContent
          locale={locale}
          heroEyebrow={copy.eyebrow}
          heroTitle={copy.title}
          heroSubtitle={copy.subtitle}
          heroIntro={copy.intro}
          releaseLabel={copy.version}
          versionPickerLabel={copy.versionPickerLabel}
          noPublishedVersions={copy.noPublishedVersions}
          releaseVersionUnavailable={copy.releaseVersionUnavailable}
        />
      </main>

      <section
        className="updatesClosingSnap"
        id="updates-download"
        data-snap-section
      >
        <div className="updatesDownloads sectionContainer">
          <Eyebrow reveal>{copy.downloadsEyebrow}</Eyebrow>
          <h2 data-text-reveal="title">{copy.downloadsTitle}</h2>
          <p>{copy.downloadsBody}</p>
          <div className="buttonRow">
            <a
              className="button buttonPrimary"
              href={microsoftStoreUrl}
              target="_blank"
              rel="noreferrer"
            >
              {copy.storeLabel}
              <ExternalArrow />
            </a>
            <a
              className="button buttonSecondary"
              href={githubUrl}
              target="_blank"
              rel="noreferrer"
            >
              GitHub
              <ExternalArrow />
            </a>
          </div>
        </div>

        <footer className="updatesFooter">
          <div className="sectionContainer">
            <LogoLockup locale={locale} />
            <p>{copy.footerNote}</p>
            <div>
              <Link href={home}>{copy.backLabel}</Link>
              <span>© 2026 {displayBrand(locale)}</span>
            </div>
          </div>
        </footer>
      </section>
    </>
  );
}
