import type { Metadata } from "next";
import { EasterSongsPage } from "@/components/EasterSongsPage";
import { easterCopy } from "@/data/easter-songs";


export const metadata: Metadata = {
  title: easterCopy.en.nav,
  description: easterCopy.en.intro,
  alternates: {
    canonical: "/en/songs/",
    languages: { "zh-CN": "/songs/", "zh-Hant": "/zh-hant/songs/", en: "/en/songs/", ja: "/ja/songs/", "x-default": "/songs/" },
  },
};

export default function SongsPage() {
  return <><EasterSongsPage locale="en" /></>;
}
