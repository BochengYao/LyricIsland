import type { Metadata } from "next";
import { SitePage } from "@/components/SitePage";
import { UnprefixedLocaleRedirect } from "@/components/UnprefixedLocaleRedirect";

export const metadata: Metadata = {
  alternates: {
    canonical: "/",
    languages: {
      "zh-CN": "/",
      "zh-Hant": "/zh-hant/",
      en: "/en/",
      ja: "/ja/",
      "x-default": "/"
    }
  }
};

export default function HomePage() {
  return <><UnprefixedLocaleRedirect /><SitePage locale="zh" /></>;
}
