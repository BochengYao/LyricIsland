import type { Metadata } from "next";
import "@fontsource-variable/sofia-sans/wght.css";
import "../globals.css";
import DevSourceLocator from "@/components/DevSourceLocator";
import { AccessLogger } from "@/components/AccessLogger";
import { SiteStructuredData } from "@/components/SiteStructuredData";

export const metadata: Metadata = {
  metadataBase: new URL("https://lyric-island.top"),
  title: {
    default: "LyricHover | LyricHover",
    template: "%s | LyricHover"
  },
  description:
    "LyricHover是一款面向 Windows 的顶部桌面歌词伴侣，支持多播放器、同步歌词、模块化布局和鼠标避让。",
  applicationName: "LyricHover",
  keywords: [
    "LyricHover",
    "LyricHover",
    "Windows desktop lyrics",
    "multi-player desktop lyrics",
    "SMTC"
  ],
  icons: {
    icon: "/images/app-logo.png",
    apple: "/images/app-logo.png"
  },
  openGraph: {
    type: "website",
    title: "LyricHover | LyricHover",
    description: "让桌面歌词安静停在屏幕顶部。",
    images: [
      {
        url: "/images/product-hero.png",
        width: 1600,
        height: 900,
        alt: "LyricHover停靠在 Windows 桌面顶部"
      }
    ]
  },
  twitter: {
    card: "summary_large_image",
    title: "LyricHover | LyricHover",
    description: "让桌面歌词安静停在屏幕顶部。",
    images: ["/images/product-hero.png"]
  }
};

export default function ChineseRootLayout({
  children
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <head>
        <SiteStructuredData />
        <meta name="baidu-site-verification" content="codeva-x0GnwjHSeW" />
        <link
          rel="preload"
          href="/fonts/xiaolai-nav-v2.woff2"
          as="font"
          type="font/woff2"
          crossOrigin="anonymous"
        />
      </head>
      <body>
        <AccessLogger />
        {children}
        {process.env.NODE_ENV === "development" ? <DevSourceLocator /> : null}
      </body>
    </html>
  );
}
