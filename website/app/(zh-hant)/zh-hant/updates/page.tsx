import type { Metadata } from "next";
import { UpdatesPage } from "@/components/UpdatesPage";

export const metadata: Metadata = {
  title: "v2.0 更新內容",
  description:
    "深入了解 LyricHover v2.0 的模組化版面、多播放器支援、時間軸策略、歌詞來源與互動更新。",
  alternates: {
    canonical: "/zh-hant/updates/",
    languages: {
      "zh-CN": "/updates/",
      "zh-Hant": "/zh-hant/updates/",
      en: "/en/updates/",
      ja: "/ja/updates/",
      "x-default": "/updates/"
    }
  }
};

export default function TraditionalChineseUpdatesPage() { return <UpdatesPage locale="zhHant" />; }
