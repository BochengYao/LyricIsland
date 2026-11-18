import type { Metadata } from "next";
import { EasterSongsPage } from "@/components/EasterSongsPage";
import { easterCopy } from "@/data/easter-songs";


export const metadata: Metadata = {
  title: easterCopy.ja.nav,
  description: easterCopy.ja.intro,
  alternates: {
    canonical: "/ja/songs/",
    languages: { "zh-CN": "/songs/", "zh-Hant": "/zh-hant/songs/", en: "/en/songs/", ja: "/ja/songs/", "x-default": "/songs/" },
  },
};

export default function SongsPage() {
  return <><EasterSongsPage locale="ja" /></>;
}
