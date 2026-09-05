import type { Metadata } from "next";
import { UpdatesPage } from "@/components/UpdatesPage";

export const metadata: Metadata = {
  title: "v2.0 Beta 1 updates | Lyric Island",
  description:
    "A detailed look at modular layouts, multi-player support, timeline strategy, lyric providers, and interaction changes in Lyric Island v2.0 Beta 1."
};

export default function EnglishUpdatesPage() {
  return <UpdatesPage locale="en" />;
}
