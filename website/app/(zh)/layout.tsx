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
    "在屏幕顶部遇见音乐。歌词岛支持鼠标避让、模块化布局、自动折叠、歌词翻译与多播放器连接。",
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
    description: "这一句，值得被看见。在屏幕顶部，遇见音乐。",
    images: [
      {
        url: "/images/campaign/hero.png",
        width: 1998,
        height: 1125,
        alt: "歌词岛停靠在 Windows 桌面顶部"
      }
    ]
  },
  twitter: {
    card: "summary_large_image",
    title: "歌词岛 | Lyric Island",
    description: "这一句，值得被看见。在屏幕顶部，遇见音乐。",
    images: ["/images/campaign/hero.png"]
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
