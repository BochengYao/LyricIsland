import Image from "next/image";
import Link from "next/link";
import { AnimatedFaqItem } from "@/components/AnimatedFaqItem";
import { ExternalArrow } from "@/components/ExternalArrow";
import { IslandDemo } from "@/components/IslandDemo";
import { ModuleComposer } from "@/components/ModuleComposer";
import { PlayerOrbit } from "@/components/PlayerOrbit";
import { SelectiveTextReveal } from "@/components/SelectiveTextReveal";
import { SmoothSectionScroll } from "@/components/SmoothSectionScroll";
import { displayBrand } from "@/lib/brand";
import {
  copyByLocale,
  microsoftStoreUrl,
  type Locale
} from "@/data/site-copy";

const githubUrl = "https://github.com/BochengYao/LyricHover";

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

export function LogoLockup({ locale = "zh" }: { locale?: Locale }) {
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
        <strong>{displayBrand(locale)}</strong>
        <small>Lyric Hover</small>
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
          <LogoLockup locale={locale} />
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
                  </div>
                  <div className="orbitCopy">
                    <Eyebrow>{item.tag}</Eyebrow>
                    <h3>{item.title}</h3>
                    <p>{item.body}</p>
                  </div>
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

        <section
          className="modulesSection"
          id="modules"
          data-snap-section
          data-staged-scroll="true"
        >
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
            <ModuleComposer
              label={copy.modules.title}
              names={copy.modules.names}
              imageAlt={copy.modules.imageAlt}
            />
          </div>
        </section>

        <section
          className="compatibilitySection"
          id="players"
          data-snap-section
          data-wheel-snap="direct"
        >
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
              <LogoLockup locale={locale} />
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
