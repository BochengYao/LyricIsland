import Link from "next/link";
import { Eyebrow, LogoLockup } from "@/components/SitePage";
import { SelectiveTextReveal } from "@/components/SelectiveTextReveal";
import {
  copyByLocale,
  microsoftStoreUrl,
  type Locale
} from "@/data/site-copy";
import { updatesByLocale } from "@/data/updates-copy";

type Props = {
  locale: Locale;
};

export function UpdatesPage({ locale }: Props) {
  const siteCopy = copyByLocale[locale];
  const copy = updatesByLocale[locale];
  const home = locale === "zh" ? "/" : "/en";

  return (
    <>
      <a className="skipLink" href="#updates-main">
        {locale === "zh" ? "跳到更新内容" : "Skip to updates"}
      </a>
      <SelectiveTextReveal />
      <header className="siteHeader">
        <nav className="floatingNav updatesNav" aria-label={copy.menuLabel}>
          <Link href={home} className="brandLink">
            <LogoLockup />
          </Link>
          <Link className="updatesBackLink" href={home}>
            <span aria-hidden="true">←</span>
            {copy.backLabel}
          </Link>
          <div className="navActions">
            <a
              className="storeNavLink"
              href={microsoftStoreUrl}
              target="_blank"
              rel="noreferrer"
            >
              Microsoft Store
            </a>
            <Link className="languageLink" href={copy.languageHref}>
              {copy.languageName}
            </Link>
          </div>
        </nav>
      </header>

      <main id="updates-main" className="updatesMain">
        <section className="updatesHero sectionContainer">
          <Eyebrow reveal>{copy.eyebrow}</Eyebrow>
          <h1 data-text-reveal="title">{copy.title}</h1>
          <p className="updatesLead">{copy.intro}</p>
          <div className="releaseMeta">
            <span>{copy.version}</span>
            <span>{copy.status}</span>
          </div>
        </section>

        <section className="updatesSummary sectionContainer">
          <span>{copy.summaryLabel}</span>
          <ul>
            {copy.summary.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>

        <section className="releaseSections sectionContainer">
          {copy.sections.map((section) => (
            <article className="releaseSection" key={section.number}>
              <span className="releaseNumber">{section.number}</span>
              <div>
                <h2>{section.title}</h2>
                <p>{section.body}</p>
              </div>
              <ul>
                {section.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </article>
          ))}
        </section>

        <section className="updatesDownloads sectionContainer">
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
              <span aria-hidden="true">↗</span>
            </a>
          </div>
        </section>
      </main>

      <footer className="updatesFooter">
        <div className="sectionContainer">
          <LogoLockup />
          <p>{copy.footerNote}</p>
          <div>
            <Link href={home}>{copy.backLabel}</Link>
            <span>© 2026 Lyric Island</span>
          </div>
        </div>
      </footer>
    </>
  );
}
