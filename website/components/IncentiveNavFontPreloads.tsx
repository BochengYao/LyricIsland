type IncentiveNavLocale = "zh" | "zhHant" | "en" | "ja";

const fontFilesByLocale: Record<IncentiveNavLocale, readonly string[]> = {
  zh: [
    "c8d953508370432b.woff2",
    "920f020d9f18f2fa.woff2",
    "6f9700fbccda7b94.woff2",
    "35b5a4f1f1ffc722.woff2",
    "f035a4b4a1e2a09a.woff2"
  ],
  zhHant: [
    "f035a4b4a1e2a09a.woff2",
    "8e752d4ce136978c.woff2",
    "d16711956507570c.woff2",
    "bed8cb62a7886a5e.woff2",
    "53565bb45a78efe1.woff2"
  ],
  en: ["2fd304065f29d43b.woff2", "4148614b760b8a30.woff2"],
  ja: [
    "af8e55683351c55d.woff2",
    "067b97abfb510ac8.woff2",
    "f035a4b4a1e2a09a.woff2",
    "974bb3880148d54d.woff2"
  ]
};

export default function IncentiveNavFontPreloads({ locale }: { locale: IncentiveNavLocale }) {
  return fontFilesByLocale[locale].map((file) => (
    <link
      key={file}
      rel="preload"
      href={`/fonts/851-lakeus-night-writing/${file}`}
      as="font"
      type="font/woff2"
      crossOrigin="anonymous"
    />
  ));
}
