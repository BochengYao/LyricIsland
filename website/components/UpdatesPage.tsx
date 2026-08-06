import Link from "next/link";
import { DatabasePreload } from "@/components/DatabasePreload";
import { ExternalArrow } from "@/components/ExternalArrow";
import { ManagedFeatureContent } from "@/components/ManagedFeatureContent";
import { Eyebrow, LogoLockup, PrimaryNavigation } from "@/components/SitePage";
import { SelectiveTextReveal } from "@/components/SelectiveTextReveal";
import { SmoothSectionScroll } from "@/components/SmoothSectionScroll";
import { VersionPreviewSection } from "@/components/VersionPreviewSection";
import {
  copyByLocale,
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
  const home = locale === "zh" ? "/" : "/en";

  return (
    <>
      <DatabasePreload href="/api/features" />
      <DatabasePreload href="/api/incentives/public" />
      <a className="skipLink" href="#updates-main">
        {locale === "zh" ? "跳到更新内容" : "Skip to updates"}
      </a>
      <SmoothSectionScroll />
      <SelectiveTextReveal />
      <PrimaryNavigation locale={locale} homeHref={home} languageHref={copy.languageHref} />

      <main id="updates-main" className="updatesMain">
        <ManagedFeatureContent
          locale={locale}
          heroEyebrow={copy.eyebrow}
          heroTitle={copy.title}
          heroIntro={copy.intro}
          releaseLabel={copy.version}
        />

        <VersionPreviewSection locale={locale} />
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
            <LogoLockup />
            <p>{copy.footerNote}</p>
            <div>
              <Link href={home}>{copy.backLabel}</Link>
              <span>© 2026 LyricHover</span>
            </div>
          </div>
        </footer>
      </section>
    </>
  );
}
