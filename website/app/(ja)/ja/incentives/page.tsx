import type { Metadata } from "next";
import { IncentivePage } from "@/components/IncentivePage";
import { incentivesByLocale } from "@/data/incentives-copy";

export const metadata: Metadata = {
  title: incentivesByLocale.ja.pageTitle,
  description: incentivesByLocale.ja.pageDescription,
  alternates: {
    canonical: "/ja/incentives/",
    languages: {
      "zh-CN": "/incentives/",
      "zh-Hant": "/zh-hant/incentives/",
      en: "/en/incentives/",
      ja: "/ja/incentives/",
      "x-default": "/incentives/"
    }
  }
};

export default function JapaneseIncentivesPage() { return <IncentivePage locale="ja" />; }
