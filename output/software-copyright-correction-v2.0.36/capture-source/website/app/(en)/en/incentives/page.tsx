import type { Metadata } from "next";
import { IncentivePage } from "@/components/IncentivePage";
import { incentivesByLocale } from "@/data/incentives-copy";

export const metadata: Metadata = {
  title: incentivesByLocale.en.pageTitle,
  description: incentivesByLocale.en.pageDescription
};

export default function EnglishIncentivesPage() {
  return <IncentivePage locale="en" />;
}
