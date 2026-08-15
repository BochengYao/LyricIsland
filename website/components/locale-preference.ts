import type { Locale } from "@/data/site-copy";

export const localePreferenceKey = "lyric_hover_locale_preference";

const validLocales: Locale[] = ["zh", "zhHant", "en", "ja"];

export function isLocale(value: string | null): value is Locale {
  return value !== null && validLocales.includes(value as Locale);
}

export function resolveBrowserLocale(
  languages: readonly string[] | undefined,
  fallbackLanguage?: string
): Locale {
  const candidates = languages?.length ? languages : fallbackLanguage ? [fallbackLanguage] : [];

  for (const candidate of candidates) {
    const language = candidate.toLowerCase().replace(/_/g, "-");
    if (language === "zh-hant" || /^zh-(tw|hk|mo)(?:-|$)/.test(language)) return "zhHant";
    if (language === "ja" || language.startsWith("ja-")) return "ja";
    if (language === "en" || language.startsWith("en-")) return "en";
    if (language === "zh" || language.startsWith("zh-")) return "zh";
  }

  return "en";
}
