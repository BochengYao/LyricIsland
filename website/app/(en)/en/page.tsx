import type { Metadata } from "next";
import { CampaignPage } from "@/components/CampaignPage";

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
  return <CampaignPage locale="en" />;
}
