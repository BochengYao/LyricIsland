import type { Metadata } from "next";
import { IncentivePage } from "@/components/IncentivePage";
import { incentivesByLocale } from "@/data/incentives-copy";

export const metadata: Metadata = {
  title: incentivesByLocale.zhHant.pageTitle,
  description: incentivesByLocale.zhHant.pageDescription,
  alternates: {
    canonical: "/zh-hant/incentives/",
    languages: {
      "zh-CN": "/incentives/",
      "zh-Hant": "/zh-hant/incentives/",
      en: "/en/incentives/",
      ja: "/ja/incentives/",
      "x-default": "/incentives/"
    }
  }
};

export default function TraditionalChineseIncentivesPage() { return <IncentivePage locale="zhHant" />; }
