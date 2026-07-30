import type { Metadata } from "next";
import { SitePage } from "@/components/SitePage";

export const metadata: Metadata = {
  alternates: {
    canonical: "/",
    languages: {
      "zh-CN": "/",
      en: "/en/",
      "x-default": "/"
    }
  }
};

export default function HomePage() {
  return <SitePage locale="zh" />;
}
