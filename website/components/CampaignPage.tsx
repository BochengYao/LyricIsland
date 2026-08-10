import Image from "next/image";
import Link from "next/link";
import styles from "./CampaignPage.module.css";
import { contentLocale, microsoftStoreUrl, type Locale } from "@/data/site-copy";

type Scene = {
  id: string;
  number: string;
  label: string;
  title: string[];
  subtitle: string;
  image: string;
  imageAlt: string;
  watermark: string;
  variant: "hero" | "mouse" | "modules" | "collapse" | "translation" | "players";
};

const scenesByLocale: Record<"zh" | "en", Scene[]> = {
  zh: [
    {
      id: "hero",
      number: "01",
      label: "主视觉",
      title: ["这一句，", "值得被看见。"],
      subtitle: "在屏幕顶部，遇见音乐。",
      image: "/images/campaign/hero.png",
      imageAlt: "歌词岛显示在 Windows 桌面顶部",
      watermark: "BE SEEN",
      variant: "hero"
    },
    {
      id: "mouse",
      number: "02",
      label: "鼠标避让",
      title: ["鼠标靠近，", "内容仍是主角。"],
      subtitle: "鼠标经过的地方，歌词会自然变淡，让下方内容保持可读、可操作。",
      image: "/images/campaign/mouse.png",
      imageAlt: "鼠标靠近时歌词岛局部变淡",
      watermark: "MAKE ROOM",
      variant: "mouse"
    },
    {
      id: "modules",
      number: "03",
      label: "模块化布局",
      title: ["想怎么展开，", "就怎么呈现。"],
      subtitle: "水平排列，简洁舒展；自动折叠，节省空间。歌词岛会随你的布局自然变化。",
      image: "/images/campaign/modular.png",
      imageAlt: "歌词岛的多种模块化布局",
      watermark: "YOUR SHAPE",
      variant: "modules"
    },
    {
      id: "collapse",
      number: "04",
      label: "自动折叠",
      title: ["一开场，", "就在场。"],
      subtitle: "音乐响起，歌词自然浮现；播放停止，便悄然收起。",
      image: "/images/campaign/collapse.png",
      imageAlt: "歌词岛停止播放后收回屏幕顶部",
      watermark: "ON CUE",
      variant: "collapse"
    },
    {
      id: "translation",
      number: "05",
      label: "歌词与翻译",
      title: ["听见原文。", "也读懂它。"],
      subtitle: "歌词与翻译，同步呈现。",
      image: "/images/campaign/translation.png",
      imageAlt: "歌词岛同时显示原文歌词和中文翻译",
      watermark: "UNDERSTAND",
      variant: "translation"
    },
    {
      id: "players",
      number: "06",
      label: "多播放器支持",
      title: ["换个播放器，", "歌词照常在场。"],
      subtitle: "熟悉的歌词岛，连接你常用的音乐应用。",
      image: "/images/campaign/players.png",
      imageAlt: "多播放器连接的抽象轨道背景",
      watermark: "STAY CONNECTED",
      variant: "players"
    }
  ],
  en: [
    {
      id: "hero",
      number: "01",
      label: "Opening",
      title: ["This line,", "deserves to be seen."],
      subtitle: "Meet the music at the top of your screen.",
      image: "/images/campaign/hero.png",
      imageAlt: "Lyric Hover at the top of a Windows desktop",
      watermark: "BE SEEN",
      variant: "hero"
    },
    {
      id: "mouse",
      number: "02",
      label: "Mouse awareness",
      title: ["When the pointer comes close,", "your content stays in focus."],
      subtitle: "Lyrics soften naturally beneath the pointer, keeping everything below readable and ready to use.",
      image: "/images/campaign/mouse.png",
      imageAlt: "Lyric Hover softening around the pointer",
      watermark: "MAKE ROOM",
      variant: "mouse"
    },
    {
      id: "modules",
      number: "03",
      label: "Modular layouts",
      title: ["Expand it your way,", "show it your way."],
      subtitle: "Stretch into a clean horizontal layout or fold away to save space. Lyric Hover follows the shape you choose.",
      image: "/images/campaign/modular.png",
      imageAlt: "Several modular Lyric Hover layouts",
      watermark: "YOUR SHAPE",
      variant: "modules"
    },
    {
      id: "collapse",
      number: "04",
      label: "Auto collapse",
      title: ["The moment music starts,", "it is already there."],
      subtitle: "Lyrics surface as the music begins, then quietly retreat when playback stops.",
      image: "/images/campaign/collapse.png",
      imageAlt: "Lyric Hover retracted above the screen",
      watermark: "ON CUE",
      variant: "collapse"
    },
    {
      id: "translation",
      number: "05",
      label: "Lyrics and translation",
      title: ["Hear the original.", "Understand it, too."],
      subtitle: "Lyrics and translation, presented in sync.",
      image: "/images/campaign/translation.png",
      imageAlt: "Original lyrics and a translation shown together",
      watermark: "UNDERSTAND",
      variant: "translation"
    },
    {
      id: "players",
      number: "06",
      label: "Player support",
      title: ["Switch players,", "the lyrics stay with you."],
      subtitle: "The Lyric Hover you know, connected to the music apps you use.",
      image: "/images/campaign/players.png",
      imageAlt: "An orbital visual representing several connected music players",
      watermark: "STAY CONNECTED",
      variant: "players"
    }
  ]
};

const playersByLocale: Record<"zh" | "en", string[]> = {
  zh: ["Apple Music", "QQ 音乐", "网易云音乐", "酷狗音乐", "Spotify", "酷我音乐"],
  en: ["Apple Music", "QQ Music", "NetEase", "Kugou Music", "Spotify", "Kuwo Music"]
};

function OrbitArc() {
  return (
    <svg className={styles.orbitArc} viewBox="0 0 1600 900" preserveAspectRatio="none" aria-hidden="true">
      <path d="M -120 720 C 280 310, 640 1030, 1020 520 S 1510 210, 1740 500" />
      <circle cx="1020" cy="520" r="7" />
    </svg>
  );
}

function SceneHeading({ scene }: { scene: Scene }) {
  return (
    <div className={styles.sceneCopy}>
      <p className={styles.eyebrow}>
        <span aria-hidden="true">•</span>
        {scene.label}
      </p>
      <h1>
        {scene.title.map((line) => (
          <span key={line}>{line}</span>
        ))}
      </h1>
      <p className={styles.subtitle}>{scene.subtitle}</p>
    </div>
  );
}

function SceneVisual({
  scene,
  locale,
  priority
}: {
  scene: Scene;
  locale: Locale;
  priority: boolean;
}) {
  if (scene.variant === "players") {
    return (
      <div className={`${styles.visual} ${styles.playersVisual}`}>
        <Image
          src={scene.image}
          alt={scene.imageAlt}
          fill
          priority={priority}
          sizes="(min-width: 1024px) 1040px, 100vw"
          className={styles.visualImage}
        />
        <div className={styles.playerConstellation} aria-label={scene.label}>
          <div className={styles.playerCore}>
            <Image src="/images/app-logo.png" alt="" width={52} height={52} />
            <span>{locale === "zh" ? "歌词岛" : "Lyric Hover"}</span>
          </div>
          {playersByLocale[contentLocale(locale)].map((player, index) => (
            <span className={styles[`player${index + 1}`]} key={player}>
              {player}
            </span>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className={`${styles.visual} ${styles[`${scene.variant}Visual`]}`}>
      <Image
        src={scene.image}
        alt={scene.imageAlt}
        fill
        priority={priority}
        sizes="(min-width: 1024px) 1120px, 100vw"
        className={styles.visualImage}
      />
      {scene.variant === "mouse" ? (
        <span className={styles.pointerBadge}>{locale === "zh" ? "局部避让" : "Local fade"}</span>
      ) : null}
      {scene.variant === "translation" ? (
        <span className={styles.translationBadge}>{locale === "zh" ? "原文 + 翻译" : "Original + translation"}</span>
      ) : null}
    </div>
  );
}

export function CampaignPage({ locale }: { locale: Locale }) {
  const scenes = scenesByLocale[contentLocale(locale)];
  const storeLabel = locale === "zh" ? "Microsoft Store" : "Microsoft Store";

  return (
    <div className={styles.campaignShell}>
      <a className={styles.skipLink} href="#hero">
        {locale === "zh" ? "跳到主要内容" : "Skip to main content"}
      </a>

      <header className={styles.siteHeader}>
        <nav className={styles.floatingNav} aria-label={locale === "zh" ? "场景导航" : "Scene navigation"}>
          <a className={styles.brand} href="#hero" aria-label={locale === "zh" ? "返回主视觉" : "Back to opening"}>
            <Image src="/images/app-logo.png" alt="" width={42} height={42} />
            <span>
              <strong>歌词岛</strong>
              <small>Lyric Hover</small>
            </span>
          </a>

          <div className={styles.sceneNav}>
            {scenes.map((scene) => (
              <a href={`#${scene.id}`} key={scene.id} aria-label={`${scene.number} ${scene.label}`}>
                <span>{scene.number}</span>
              </a>
            ))}
          </div>

          <div className={styles.navActions}>
            <Link className={styles.languageLink} href={locale === "zh" ? "/en" : "/"}>
              {locale === "zh" ? "EN" : "中文"}
            </Link>
            <a className={styles.storeButton} href={microsoftStoreUrl} target="_blank" rel="noreferrer">
              {storeLabel}
              <span aria-hidden="true">↗</span>
            </a>
          </div>
        </nav>
      </header>

      <main>
        {scenes.map((scene, index) => (
          <section
            className={`${styles.scene} ${styles[scene.variant]}`}
            id={scene.id}
            key={scene.id}
            aria-labelledby={`${scene.id}-title`}
          >
            <div className={styles.frame}>
              <OrbitArc />
              <span className={styles.watermark} aria-hidden="true">{scene.watermark}</span>
              <div className={styles.frameGrid}>
                <div id={`${scene.id}-title`}>
                  <SceneHeading scene={scene} />
                  {index === 0 ? (
                    <div className={styles.heroActions}>
                      <a className={styles.primaryButton} href={microsoftStoreUrl} target="_blank" rel="noreferrer">
                        {locale === "zh" ? "在 Microsoft Store 获取" : "Get it from Microsoft Store"}
                        <span aria-hidden="true">↗</span>
                      </a>
                      <a className={styles.secondaryButton} href="#mouse">
                        {locale === "zh" ? "继续浏览" : "Keep exploring"}
                        <span aria-hidden="true">↓</span>
                      </a>
                    </div>
                  ) : null}
                </div>

                <SceneVisual scene={scene} locale={locale} priority={index === 0} />
              </div>

              <div className={styles.sceneMeta}>
                <span>{scene.number}</span>
                <span>{scene.label}</span>
              </div>
              {index === scenes.length - 1 ? (
                <p className={styles.legal}>
                  © 2026 {locale === "zh" ? "歌词岛" : "Lyric Hover"} · {locale === "zh"
                    ? "播放器名称及商标归各自权利人所有。"
                    : "Player names and trademarks belong to their respective owners."}
                </p>
              ) : null}
            </div>
          </section>
        ))}
      </main>
    </div>
  );
}
