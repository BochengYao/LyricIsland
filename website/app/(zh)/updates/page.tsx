import type { Metadata } from "next";
import { UpdatesPage } from "@/components/UpdatesPage";

export const metadata: Metadata = {
  title: "v2.0 更新内容",
  description:
    "详细了解LyricHover v2.0 的模块化布局、多播放器支持、时间轴策略、歌词源与交互更新。",
  alternates: {
    canonical: "/updates/",
    languages: {
      "zh-CN": "/updates/",
      en: "/en/updates/",
      "x-default": "/updates/"
    }
  }
};

export default function ChineseUpdatesPage() {
  return <UpdatesPage locale="zh" />;
}
