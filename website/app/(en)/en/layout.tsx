import type { Metadata } from "next";
import "@fontsource-variable/sofia-sans/wght.css";
import "../../globals.css";
import DevSourceLocator from "@/components/DevSourceLocator";
import { AccessLogger } from "@/components/AccessLogger";
import { SiteStructuredData } from "@/components/SiteStructuredData";

export const metadata: Metadata = {
  metadataBase: new URL("https://lyric-island.top"),
  title: "Windows desktop lyrics, quietly above your work | LyricHover",
  description:
    "LyricHover is a top-edge Windows lyrics companion with multi-player support, synced lyrics, modular layouts, and mouse-aware transparency.",
  applicationName: "LyricHover",
  icons: {
    icon: "/images/app-logo.png",
    apple: "/images/app-logo.png"
  },
  openGraph: {
    type: "website",
    title: "LyricHover",
    description: "Windows desktop lyrics, quietly above your work.",
    images: [
      {
        url: "/images/product-hero.png",
        width: 1600,
        height: 900,
        alt: "LyricHover at the top of a Windows desktop"
      }
    ]
  },
  twitter: {
    card: "summary_large_image",
    title: "LyricHover",
    description: "Windows desktop lyrics, quietly above your work.",
    images: ["/images/product-hero.png"]
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
        <SiteStructuredData />
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
