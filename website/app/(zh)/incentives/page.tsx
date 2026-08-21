import type { Metadata } from "next";
import { IncentivePage } from "@/components/IncentivePage";
import { incentivesByLocale } from "@/data/incentives-copy";
import { UnprefixedLocaleRedirect } from "@/components/UnprefixedLocaleRedirect";

export const metadata: Metadata = {
  title: incentivesByLocale.zh.pageTitle,
  description: incentivesByLocale.zh.pageDescription,
  alternates: {
    canonical: "/incentives/",
    languages: {
      "zh-CN": "/incentives/",
      "zh-Hant": "/zh-hant/incentives/",
      en: "/en/incentives/",
      ja: "/ja/incentives/",
      "x-default": "/incentives/"
    }
  }
};

export default function ChineseIncentivesPage() {
  return <><UnprefixedLocaleRedirect /><IncentivePage locale="zh" /></>;
}
