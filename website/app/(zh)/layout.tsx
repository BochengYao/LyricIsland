import type { Metadata } from "next";
import "@fontsource-variable/sofia-sans/wght.css";
import "../globals.css";
import DevSourceLocator from "@/components/DevSourceLocator";
import { AccessLogger } from "@/components/AccessLogger";

export const metadata: Metadata = {
  metadataBase: new URL("https://lyric-island-windows.kyc869bdc4.chatgpt.site"),
  title: {
    default: "歌词岛 | Lyric Island",
    template: "%s | Lyric Island"
  },
  description:
    "歌词岛是一款面向 Windows 的顶部桌面歌词伴侣，支持多播放器、同步歌词、模块化布局和鼠标避让。",
  applicationName: "Lyric Island",
  keywords: [
    "歌词岛",
    "Lyric Island",
    "Windows desktop lyrics",
    "multi-player desktop lyrics",
    "SMTC"
  ],
  alternates: {
    languages: {
      "zh-CN": "/",
      en: "/en"
    }
  },
  icons: {
    icon: "/images/app-logo.png",
    apple: "/images/app-logo.png"
  },
  openGraph: {
    type: "website",
    title: "歌词岛 | Lyric Island",
    description: "让桌面歌词安静停在屏幕顶部。",
    images: [
      {
        url: "/images/product-hero.png",
        width: 1600,
        height: 900,
        alt: "歌词岛停靠在 Windows 桌面顶部"
      }
    ]
  },
  twitter: {
    card: "summary_large_image",
    title: "歌词岛 | Lyric Island",
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
        <link
          rel="preload"
          href="/fonts/xiaolai-nav.woff2"
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
