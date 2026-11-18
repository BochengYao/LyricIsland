import type { Metadata } from "next";
import { EasterSongsPage } from "@/components/EasterSongsPage";
import { easterCopy } from "@/data/easter-songs";
import { UnprefixedLocaleRedirect } from "@/components/UnprefixedLocaleRedirect";

export const metadata: Metadata = {
  title: easterCopy.zh.nav,
  description: easterCopy.zh.intro,
  alternates: {
    canonical: "/songs/",
    languages: { "zh-CN": "/songs/", "zh-Hant": "/zh-hant/songs/", en: "/en/songs/", ja: "/ja/songs/", "x-default": "/songs/" },
  },
};

export default function SongsPage() {
  return <><UnprefixedLocaleRedirect /><EasterSongsPage locale="zh" /></>;
}
