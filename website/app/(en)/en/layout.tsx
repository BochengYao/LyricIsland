import type { Metadata } from "next";
import "@fontsource-variable/sofia-sans/wght.css";
import "../../globals.css";
import DevSourceLocator from "@/components/DevSourceLocator";
import { AccessLogger } from "@/components/AccessLogger";

export const metadata: Metadata = {
  metadataBase: new URL("https://lyric-island-windows.kyc869bdc4.chatgpt.site"),
  title: "This line deserves to be seen | Lyric Island",
  description:
    "Meet music at the top of your screen with mouse-aware transparency, modular layouts, auto collapse, translation, and multi-player support.",
  applicationName: "Lyric Island",
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
    title: "Lyric Island",
    description: "This line deserves to be seen. Meet the music at the top of your screen.",
    images: [
      {
        url: "/images/campaign/hero.png",
        width: 1998,
        height: 1125,
        alt: "Lyric Island at the top of a Windows desktop"
      }
    ]
  },
  twitter: {
    card: "summary_large_image",
    title: "Lyric Island",
    description: "This line deserves to be seen. Meet the music at the top of your screen.",
    images: ["/images/campaign/hero.png"]
  }
};

export default function EnglishRootLayout({
  children
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
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
