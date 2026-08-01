import type { Metadata } from "next";
import { IncentivePage } from "@/components/IncentivePage";
import { incentivesByLocale } from "@/data/incentives-copy";

export const metadata: Metadata = {
  title: incentivesByLocale.zh.pageTitle,
  description: incentivesByLocale.zh.pageDescription,
  alternates: {
    canonical: "/incentives/",
    languages: {
      "zh-CN": "/incentives/",
      en: "/en/incentives/",
      "x-default": "/incentives/"
    }
  }
};

export default function ChineseIncentivesPage() {
  return <IncentivePage locale="zh" />;
}
