import type { Metadata } from "next";
import { UpdatesPage } from "@/components/UpdatesPage";

export const metadata: Metadata = {
  title: "v2.0 アップデート | LyricHover",
  description:
    "LyricHover v2.0 のモジュール式レイアウト、複数プレーヤー対応、タイムライン、歌詞ソース、操作性の更新をご紹介します。",
  alternates: {
    canonical: "/ja/updates/",
    languages: {
      "zh-CN": "/updates/",
      "zh-Hant": "/zh-hant/updates/",
      en: "/en/updates/",
      ja: "/ja/updates/",
      "x-default": "/updates/"
    }
  }
};

export default function JapaneseUpdatesPage() { return <UpdatesPage locale="ja" />; }
