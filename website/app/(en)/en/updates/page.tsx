import type { Metadata } from "next";
import { UpdatesPage } from "@/components/UpdatesPage";

export const metadata: Metadata = {
  title: "v2.0 updates | LyricHover",
  description:
    "A detailed look at modular layouts, multi-player support, timeline strategy, lyric providers, and interaction changes in LyricHover v2.0.",
  alternates: {
    canonical: "/en/updates/",
    languages: {
      "zh-CN": "/updates/",
      en: "/en/updates/",
      "x-default": "/updates/"
    }
  }
};

export default function EnglishUpdatesPage() {
  return <UpdatesPage locale="en" />;
}
