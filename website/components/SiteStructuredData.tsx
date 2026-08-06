import type { Locale } from "@/data/site-copy";

const siteUrl = "https://lyric-island.top";

export function SiteStructuredData({ locale }: { locale: Locale }) {
  const chinese = locale === "zh";
  const siteName = chinese ? "歌词岛 | Lyric Hover" : "Lyric Hover";
  const appName = chinese ? "歌词岛" : "Lyric Hover";
  const description = chinese
    ? "歌词岛是一款面向 Windows 的顶部桌面歌词伴侣，支持多播放器、同步歌词、模块化布局和鼠标避让。"
    : "Lyric Hover is a top-edge Windows lyrics companion with multi-player support, synced lyrics, modular layouts, and mouse-aware transparency.";
  const structuredData = {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "WebSite",
        "@id": `${siteUrl}/#website`,
        url: `${siteUrl}/`,
        name: siteName,
        alternateName: chinese ? ["歌词岛", "Lyric Hover"] : ["Lyric Hover", "歌词岛"],
        inLanguage: ["zh-CN", "en"]
      },
      {
        "@type": "SoftwareApplication",
        "@id": `${siteUrl}/#software`,
        name: appName,
        alternateName: chinese ? "Lyric Hover" : "歌词岛",
        url: `${siteUrl}/`,
        image: `${siteUrl}/images/app-logo.png`,
        description,
        applicationCategory: "MultimediaApplication",
        operatingSystem: "Windows",
        inLanguage: ["zh-CN", "en"]
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
