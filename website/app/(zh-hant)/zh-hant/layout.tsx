import type { Metadata } from "next";
import "@fontsource-variable/sofia-sans/wght.css";
import "../../globals.css";
import { AccessLogger } from "@/components/AccessLogger";
import DevSourceLocator from "@/components/DevSourceLocator";
import IncentiveNavFontPreloads from "@/components/IncentiveNavFontPreloads";
import { SiteStructuredData } from "@/components/SiteStructuredData";

export const metadata: Metadata = {
  metadataBase: new URL("https://lyric-island.top"),
  title: { default: "歌詞島 | LyricHover", template: "%s | 歌詞島 | LyricHover" },
  description: "歌詞島 | LyricHover 是 Windows 螢幕頂端的桌面歌詞夥伴，支援多播放器、同步歌詞、模組化版面與滑鼠避讓。",
  applicationName: "歌詞島 | LyricHover",
  alternates: { canonical: "/zh-hant/", languages: { "zh-CN": "/", "zh-Hant": "/zh-hant/", en: "/en/", ja: "/ja/", "x-default": "/" } }
};

export default function TraditionalChineseRootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="zh-Hant"><head><SiteStructuredData locale="zhHant" /><IncentiveNavFontPreloads locale="zhHant" /></head><body><AccessLogger />{children}{process.env.NODE_ENV === "development" ? <DevSourceLocator /> : null}</body></html>;
}
