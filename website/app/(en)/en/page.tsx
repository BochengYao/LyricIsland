import type { Metadata } from "next";
import { SitePage } from "@/components/SitePage";

export const metadata: Metadata = {
  alternates: {
    canonical: "/en/",
    languages: {
      "zh-CN": "/",
      en: "/en/",
      "x-default": "/"
    }
  }
};

export default function EnglishHomePage() {
  return <SitePage locale="en" />;
}
