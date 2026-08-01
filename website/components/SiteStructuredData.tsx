const siteUrl = "https://lyric-island.top";

const structuredData = {
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "WebSite",
      "@id": `${siteUrl}/#website`,
      url: `${siteUrl}/`,
      name: "歌词岛 | Lyric Island",
      alternateName: ["歌词岛", "Lyric Island"],
      inLanguage: ["zh-CN", "en"]
    },
    {
      "@type": "SoftwareApplication",
      "@id": `${siteUrl}/#software`,
      name: "歌词岛",
      alternateName: "Lyric Island",
      url: `${siteUrl}/`,
      image: `${siteUrl}/images/app-logo.png`,
      description:
        "歌词岛是一款面向 Windows 的顶部桌面歌词伴侣，支持多播放器、同步歌词、模块化布局和鼠标避让。",
      applicationCategory: "MultimediaApplication",
      operatingSystem: "Windows",
      inLanguage: ["zh-CN", "en"]
    }
  ]
};

export function SiteStructuredData() {
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{
        __html: JSON.stringify(structuredData).replace(/</g, "\\u003c")
      }}
    />
  );
}
