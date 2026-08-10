import { displayBrand, isChineseLocale, type Locale } from "@/data/site-copy";

const siteUrl = "https://lyric-island.top";

export function SiteStructuredData({ locale }: { locale: Locale }) {
  const chinese = isChineseLocale(locale);
  const siteName = chinese ? `${displayBrand(locale)} | LyricHover` : "LyricHover";
  const appName = displayBrand(locale);
  const description = locale === "zhHant"
    ? "歌詞島 | LyricHover 是 Windows 螢幕頂端的桌面歌詞夥伴，支援多播放器、同步歌詞、模組化版面與滑鼠避讓。"
    : locale === "ja"
      ? "LyricHover は、複数プレーヤー、同期歌詞、モジュールレイアウト、マウス回避に対応した Windows 用デスクトップ歌詞コンパニオンです。"
      : chinese
        ? "歌词岛是一款面向 Windows 的顶部桌面歌词伴侣，支持多播放器、同步歌词、模块化布局和鼠标避让。"
        : "LyricHover is a top-edge Windows lyrics companion with multi-player support, synced lyrics, modular layouts, and mouse-aware transparency.";
  const structuredData = {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "WebSite",
        "@id": `${siteUrl}/#website`,
        url: `${siteUrl}/`,
        name: siteName,
        alternateName: ["歌词岛", "歌詞島", "LyricHover"],
        inLanguage: ["zh-CN", "zh-Hant", "en", "ja"]
      },
      {
        "@type": "SoftwareApplication",
        "@id": `${siteUrl}/#software`,
        name: appName,
        alternateName: chinese ? "LyricHover" : "歌词岛 | 歌詞島",
        url: `${siteUrl}/`,
        image: `${siteUrl}/images/app-logo.png`,
        description,
        applicationCategory: "MultimediaApplication",
        operatingSystem: "Windows",
        inLanguage: ["zh-CN", "zh-Hant", "en", "ja"]
      }
    ]
  };

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{
        __html: JSON.stringify(structuredData).replace(/</g, "\\u003c")
      }}
    />
  );
}
