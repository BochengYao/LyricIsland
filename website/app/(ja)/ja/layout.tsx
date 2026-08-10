import type { Metadata } from "next";
import "@fontsource-variable/sofia-sans/wght.css";
import "../../globals.css";
import { AccessLogger } from "@/components/AccessLogger";
import DevSourceLocator from "@/components/DevSourceLocator";
import IncentiveNavFontPreloads from "@/components/IncentiveNavFontPreloads";
import { SiteStructuredData } from "@/components/SiteStructuredData";

export const metadata: Metadata = {
  metadataBase: new URL("https://lyric-island.top"),
  title: { default: "LyricHover | Windows デスクトップ歌詞", template: "%s | LyricHover" },
  description: "LyricHover は、複数プレーヤー、同期歌詞、モジュールレイアウト、マウス回避に対応した Windows 用デスクトップ歌詞コンパニオンです。",
  applicationName: "LyricHover",
  alternates: { canonical: "/ja/", languages: { "zh-CN": "/", "zh-Hant": "/zh-hant/", en: "/en/", ja: "/ja/", "x-default": "/" } }
};

export default function JapaneseRootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="ja"><head><SiteStructuredData locale="ja" /><IncentiveNavFontPreloads locale="ja" /></head><body><AccessLogger />{children}{process.env.NODE_ENV === "development" ? <DevSourceLocator /> : null}</body></html>;
}
