import Link from "next/link";
import { PrimaryNavigation, LogoLockup, Eyebrow } from "./SitePage";
import { EasterSongsExplorer } from "./EasterSongsExplorer";
import { ExternalArrow } from "./ExternalArrow";
import { DecorativeStar } from "./DecorativeStar";
import { easterCopy } from "@/data/easter-songs";
import { localePath, microsoftStoreUrl, type Locale } from "@/data/site-copy";
import styles from "./EasterSongsPage.module.css";

export function EasterSongsPage({ locale }: { locale: Locale }) {
  const copy = easterCopy[locale];
  return <>
    <a className="skipLink" href="#main">{copy.skip}</a>
    <PrimaryNavigation locale={locale} page="songs" />
    <main id="main" className={`${styles.main} sectionContainer`}>
      <div className={styles.hero}>
        <div><Eyebrow>{copy.eyebrow}</Eyebrow><h1>{copy.title}</h1></div>
        <div className={styles.heroNote}><DecorativeStar className={styles.asterisk} /><p>{copy.intro}</p></div>
      </div>
      <EasterSongsExplorer locale={locale} />
      <section className={styles.guide} aria-labelledby="how-title">
        <div><Eyebrow>FROM PLAYLIST TO DESKTOP</Eyebrow><h2 id="how-title">{copy.how}</h2><a className="button buttonPrimary" href={microsoftStoreUrl} target="_blank" rel="noreferrer">{copy.download}<ExternalArrow /></a></div>
        <div><ol>{copy.steps.map((step, index) => <li key={step}><span>{String(index + 1).padStart(2, "0")}</span><p>{step}</p></li>)}</ol></div>
      </section>
    </main>
    <footer className={styles.footer}><div className="sectionContainer"><LogoLockup locale={locale}/><p>{copy.footer}</p><Link href={localePath(locale, "home")}>{copy.home} ↗</Link></div></footer>
  </>;
}
