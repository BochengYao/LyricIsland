import type { Metadata } from "next";
import { CampaignPage } from "@/components/CampaignPage";

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
  return <CampaignPage locale="zh" />;
}
