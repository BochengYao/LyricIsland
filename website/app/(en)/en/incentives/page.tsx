import type { Metadata } from "next";
import { IncentivePage } from "@/components/IncentivePage";
import { incentivesByLocale } from "@/data/incentives-copy";

export const metadata: Metadata = {
  title: incentivesByLocale.en.pageTitle,
  description: incentivesByLocale.en.pageDescription,
  alternates: {
    canonical: "/en/incentives/",
    languages: {
      "zh-CN": "/incentives/",
      en: "/en/incentives/",
      "x-default": "/incentives/"
    }
  }
};

export default function EnglishIncentivesPage() {
  return <IncentivePage locale="en" />;
}
