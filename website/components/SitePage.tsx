import Image from "next/image";
import Link from "next/link";
import { AnimatedFaqItem } from "@/components/AnimatedFaqItem";
import { ExternalArrow } from "@/components/ExternalArrow";
import { IslandDemo } from "@/components/IslandDemo";
import { PlayerOrbit } from "@/components/PlayerOrbit";
import { SelectiveTextReveal } from "@/components/SelectiveTextReveal";
import { SmoothSectionScroll } from "@/components/SmoothSectionScroll";
import {
  copyByLocale,
  microsoftStoreUrl,
  type Locale
} from "@/data/site-copy";

const githubUrl = "https://github.com/BochengYao/AppleMusicDesktopLyrics";

const moduleIconPaths = [
  "M2,2 L16,2 L16,16 L2,16 Z M4,13 L8,8 L11,11 L13,9 L15,13 Z M12,5 A2,2 0 1 1 11.99,5 Z",
  "M2,3 L7,3 L7,8 L4,8 L4,11 L2,11 Z M10,3 L15,3 L15,8 L12,8 L12,11 L10,11 Z M2,14 L15,14 L15,16 L2,16 Z",
  "M3,2 L3,16 L13,9 Z M14,2 L16,2 L16,16 L14,16 Z",
  "M2,2 L16,2 L16,5 L2,5 Z M5,7 A2,2 0 1 1 4.99,7 Z M1,15 C1,12.5 3,11 5,11 C7,11 9,12.5 9,15 Z M11,9 L17,9 L17,11 L11,11 Z M11,13 L16,13 L16,15 L11,15 Z",
  "M2,8 L16,8 L16,10 L2,10 Z M9,6 A3,3 0 1 1 8.99,6 Z",
  "M8,1 L10,1 L10,17 L8,17 Z"
];

type Props = {
  locale: Locale;
};

export function Eyebrow({
  children,
  reveal = false
}: {
  children: React.ReactNode;
  reveal?: boolean;
}) {
  return (
    <p className="eyebrow" data-text-reveal={reveal ? "eyebrow" : undefined}>
      <span aria-hidden="true">•</span>
      {children}
    </p>
  );
}

export function LogoLockup() {
  return (
    <span className="logoLockup">
      <Image
        src="/images/app-logo.png"
        alt=""
        width={42}
        height={42}
        className="logoImage"
        priority
      />
      <span>
        <strong>歌词岛</strong>
        <small>Lyric Island</small>
      </span>
    </span>
  );
}

export function PrimaryNavigation({
  locale,
  homeHref = locale === "zh" ? "/" : "/en",
  languageHref
}: {
  locale: Locale;
  homeHref?: string;
  languageHref?: string;
}) {
  const copy = copyByLocale[locale];
  const localizedLanguageHref = languageHref ?? copy.languageHref;
  const navigation = copy.nav.map((item) => item.href === "#main" ? { ...item, href: homeHref } : item);

  const renderLabel = (item: (typeof navigation)[number]) => (
    <>
      {item.kind === "feature" ? (
        <span className="navFeatureText" aria-label={item.label}>
          {Array.from(item.label).map((character, index) => (
            <span aria-hidden="true" key={`${character}-${index}`}>{character}</span>
          ))}
        </span>
      ) : item.label}
      {item.kind === "store" && (
        <ExternalArrow className="navExternalArrow" variant="nav" />
      )}
    </>
  );

  return (
    <header className="siteHeader">
      <nav className="floatingNav" aria-label={copy.navLabel}>
        <Link href={locale === "zh" ? "/" : "/en"} className="brandLink">
          <LogoLockup />
        </Link>

        <div className="desktopNavLinks">
          {navigation.map((item) => (
            <a
              key={item.href}
              href={item.href}
              className={
                item.kind === "feature"
                  ? "navFeatureLink"
                  : item.kind === "store"
                    ? "navStoreLink"
                    : undefined
              }
              target={item.external ? "_blank" : undefined}
              rel={item.external ? "noreferrer" : undefined}
            >
              {renderLabel(item)}
            </a>
          ))}
        </div>

        <div className="navActions">
          <Link className="languageLink" href={localizedLanguageHref} hrefLang={locale === "zh" ? "en" : "zh-CN"}>
            {copy.languageName}
          </Link>
          <details className="mobileMenu">
            <summary aria-label={copy.menuLabel}>
              <span />
              <span />
            </summary>
            <div className="mobileMenuPanel">
              {navigation.map((item) => (
                <a
                  key={item.href}
                  href={item.href}
                  className={
                    item.kind === "feature"
                      ? "navFeatureLink"
                      : item.kind === "store"
                        ? "navStoreLink"
                        : undefined
                  }
                  target={item.external ? "_blank" : undefined}
                  rel={item.external ? "noreferrer" : undefined}
                >
                  {renderLabel(item)}
                </a>
              ))}
              <Link href={localizedLanguageHref} hrefLang={locale === "zh" ? "en" : "zh-CN"}>
                {copy.languageName}
              </Link>
            </div>
          </details>
        </div>
      </nav>
    </header>
  );
}

export function SitePage({ locale }: Props) {
  const copy = copyByLocale[locale];

  return (
    <>
      <a className="skipLink" href="#main">
        {locale === "zh" ? "跳到主要内容" : "Skip to main content"}
      </a>

      <SmoothSectionScroll />
      <SelectiveTextReveal />

      <PrimaryNavigation locale={locale} homeHref="#main" />

      <main id="main">
        <section className="hero sectionContainer" data-snap-section>
          <div className="heroGrid">
            <div>
              <h1 data-text-reveal="title">{copy.heroTitle}</h1>
            </div>
            <div className="heroSupport">
              <p>{copy.heroBody}</p>
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
              </div>
            </div>
          </div>

          <div className="heroMedia">
            <Image
              src="/images/product-hero-v2-2x.png"
              alt={copy.heroImageAlt}
              width={4000}
              height={1334}
              unoptimized
              priority
              sizes="(min-width: 1440px) 1280px, calc(100vw - 48px)"
              className="heroMediaImage"
            />
            <span className="heroBadge">{copy.heroBadge}</span>
          </div>
        </section>

        <section className="experienceSection" id="experience" data-snap-section>
          <div className="sectionContainer experienceIntro">
            {copy.experience.eyebrow && (
              <Eyebrow reveal>{copy.experience.eyebrow}</Eyebrow>
            )}
            <div className="sectionTitleGrid">
              <h2 data-text-reveal="title">{copy.experience.title}</h2>
              <p>{copy.experience.body}</p>
            </div>
          </div>

          <div className="orbitCanvas">
            <span className="watermark" aria-hidden="true">
              {copy.experience.watermark}
            </span>
            <svg
              className="orbitLine"
              viewBox="0 0 1440 920"
              preserveAspectRatio="none"
              aria-hidden="true"
            >
              <path d="M -80 390 C 220 80, 460 760, 730 420 S 1190 70, 1530 470" />
              <path d="M 80 850 C 380 610, 640 940, 910 690 S 1260 520, 1480 780" />
            </svg>
            <div className="orbitCards sectionContainer">
              {copy.experience.items.map((item, index) => (
                <article className={"orbitCard orbitCard" + (index + 1)} key={item.title}>
                  <div className="portraitWrap">
                    <Image
                      src={item.image}
                      alt={item.imageAlt}
                      fill
                      sizes="(max-width: 767px) 240px, 320px"
                      className="portraitImage"
                      style={{ objectPosition: item.imagePosition }}
                    />
                    <a className="satelliteButton" href="#demo" aria-label={item.title}>
                      <span aria-hidden="true">↘</span>
                    </a>
                  </div>
                  <Eyebrow>{item.tag}</Eyebrow>
                  <h3>{item.title}</h3>
                  <p>{item.body}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="demoSection" id="demo" data-snap-section>
          <div className="sectionContainer sectionTitleGrid">
            <div>
              <Eyebrow reveal>{copy.demo.eyebrow}</Eyebrow>
              <h2 data-text-reveal="title">{copy.demo.title}</h2>
            </div>
            <p>{copy.demo.body}</p>
          </div>
          <div className="sectionContainer">
            <IslandDemo copy={copy.demo} />
          </div>
        </section>

        <section className="modulesSection" id="modules" data-snap-section>
          <div className="sectionContainer modulesGrid">
            <div className="modulesCopy">
              <Eyebrow reveal>{copy.modules.eyebrow}</Eyebrow>
              <h2 data-text-reveal="title">
                {copy.modules.title.split("\n").map((line, index) => (
                  <span
                    className={
                      locale === "zh" && index === 1
                        ? "modulesTitleLine modulesTitleLineNoWrap"
                        : "modulesTitleLine"
                    }
                    key={line}
                  >
                    {line}
                  </span>
                ))}
              </h2>
              <p>{copy.modules.body}</p>
            </div>
            <div className="moduleComposer" aria-label={copy.modules.title}>
              <div className="moduleIsland">
                {copy.modules.names.slice(0, 4).map((name, index) => (
                  <span className={"moduleBlock moduleBlock" + (index + 1)} key={name}>
                    <i aria-hidden="true" />
                    {name}
                  </span>
                ))}
              </div>
              <div className="moduleTray">
                {copy.modules.names.map((name, index) => (
                  <span key={name}>
                    <svg
                      className="moduleTrayIcon"
                      viewBox="0 0 18 18"
                      aria-hidden="true"
                    >
                      <path
                        d={moduleIconPaths[index]}
                        fillRule="evenodd"
                        clipRule="evenodd"
                      />
                    </svg>
                    {name}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section className="compatibilitySection" id="players" data-snap-section>
          <div className="sectionContainer compatibilityInner">
            <div className="sectionTitleGrid">
              <h2 data-text-reveal="title">{copy.compatibility.title}</h2>
              <div>
                <p>{copy.compatibility.body}</p>
                <p className="finePrint">{copy.compatibility.note}</p>
              </div>
            </div>
            <PlayerOrbit label={copy.compatibility.title} players={copy.compatibility.players} />
          </div>
        </section>

        <section className="sourcesSection" data-snap-section>
          <div className="sectionContainer sourcesPanel">
            <div className="sourcesIntro">
              <Eyebrow reveal>{copy.sources.eyebrow}</Eyebrow>
              <h2 data-text-reveal="title">{copy.sources.title}</h2>
              <p>{copy.sources.body}</p>
            </div>
            <div className="factList">
                {copy.sources.facts.map((fact) => (
                  <article key={fact.label}>
                    <strong>{fact.value}</strong>
                    <h3>{fact.label}</h3>
                    <p>{fact.detail}</p>
                  </article>
                ))}
                <p className="sourcesNote">{copy.sources.note}</p>
              </div>
          </div>
        </section>

        <section className="faqSection" id="faq" data-snap-section>
          <div className="sectionContainer faqGrid">
            <div>
              <Eyebrow reveal>{copy.faq.eyebrow}</Eyebrow>
              <h2 data-text-reveal="title">{copy.faq.title}</h2>
            </div>
            <div className="faqList">
              {copy.faq.items.map((item) => (
                <AnimatedFaqItem
                  key={item.question}
                  question={item.question}
                  answer={item.answer}
                />
              ))}
            </div>
          </div>
        </section>

        <section className="closingSection" data-snap-section>
          <div className="sectionContainer closingPanel">
            <Eyebrow reveal>{copy.closing.eyebrow}</Eyebrow>
            <h2 data-text-reveal="title">{copy.closing.title}</h2>
            <p>{copy.closing.body}</p>
              <div className="buttonRow">
                <a
                  className="button buttonPrimary"
                  href={githubUrl}
                  target="_blank"
                  rel="noreferrer"
                >
                  {copy.closing.button}
                  <ExternalArrow />
                </a>
                <a
                  className="button buttonSecondary"
                  href={microsoftStoreUrl}
                  target="_blank"
                  rel="noreferrer"
                >
                  {copy.closing.storeButton}
                  <ExternalArrow />
                </a>
            </div>
          </div>
        </section>
      </main>

      <footer className="siteFooter" data-snap-section>
        <div className="sectionContainer">
          <div className="footerTop">
            <div>
              <LogoLockup />
              <h2>{copy.footer.title}</h2>
            </div>
            <div className="footerColumn">
              <h3>{copy.footer.product}</h3>
              {copy.footer.productLinks.map((link) => (
                <a href={link.href} key={link.href}>
                  {link.label}
                </a>
              ))}
            </div>
            <div className="footerColumn">
              <h3>{copy.footer.resources}</h3>
              {copy.footer.resourceLinks.map((link) =>
                link.href.startsWith("http") ? (
                  <a href={link.href} target="_blank" rel="noreferrer" key={link.href}>
                    {link.label}
                    <ExternalArrow />
                  </a>
                ) : (
                  <Link href={link.href} key={link.href}>
                    {link.label}
                  </Link>
                )
              )}
            </div>
          </div>
          <div className="footerBottom">
            <span>{copy.footer.copyright}</span>
            <span>{copy.footer.note}</span>
          </div>
        </div>
      </footer>
    </>
  );
}
