import type { Locale } from "@/data/site-copy";

export function displayBrand(locale: Locale) {
  return locale === "zh" ? "歌词岛" : "Lyric Hover";
}

export function normalizeBrandText(value: string, locale: Locale) {
  const name = displayBrand(locale);
  return value
    .replace(/\bLyric\s*Island\b/gi, name)
    .replace(/\bLyricsIsland\b/gi, name)
    .replace(/\bLyricHover\b/g, name)
    .replace(/\bthe real on-screen island\b/gi, `the real ${name}`)
    .replace(/\bthe real island\b/gi, `the real ${name}`)
    .replace(/\btop-edge island\b/gi, name)
    .replace(/\bthe whole island\b/gi, name)
    .replace(/\bthe island\b/gi, name);
}

export function normalizeBrandCopy<T>(value: T, locale: Locale): T {
  if (typeof value === "string") return normalizeBrandText(value, locale) as T;
  if (Array.isArray(value)) return value.map((item) => normalizeBrandCopy(item, locale)) as T;
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([key, item]) => [key, normalizeBrandCopy(item, locale)])
    ) as T;
  }
  return value;
}
