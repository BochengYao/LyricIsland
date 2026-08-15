import Script from "next/script";

const localeEntryRedirectScript = String.raw`(() => {
  const pathname = window.location.pathname;
  if (pathname !== "/" && pathname !== "/updates" && pathname !== "/updates/" && pathname !== "/incentives" && pathname !== "/incentives/") return;

  const preferenceKey = "lyric_hover_locale_preference";
  const supportedLocales = ["zh", "zhHant", "en", "ja"];
  let locale = null;

  try {
    const preference = window.localStorage.getItem(preferenceKey);
    if (supportedLocales.includes(preference)) locale = preference;
  } catch (_) {
    // Privacy settings can disable storage; the browser language remains a safe fallback.
  }

  if (!locale) {
    const languages = Array.isArray(navigator.languages) && navigator.languages.length
      ? navigator.languages
      : [navigator.language];

    for (const candidate of languages) {
      const language = String(candidate || "").toLowerCase().replace(/_/g, "-");
      if (language === "zh-hant" || /^zh-(tw|hk|mo)(?:-|$)/.test(language)) {
        locale = "zhHant";
        break;
      }
      if (language === "ja" || language.startsWith("ja-")) {
        locale = "ja";
        break;
      }
      if (language === "en" || language.startsWith("en-")) {
        locale = "en";
        break;
      }
      if (language === "zh" || language.startsWith("zh-")) {
        locale = "zh";
        break;
      }
    }
  }

  if (!locale) locale = "en";
  const prefix = locale === "zh" ? "" : locale === "zhHant" ? "/zh-hant" : locale === "ja" ? "/ja" : "/en";
  const destination = prefix + pathname + window.location.search + window.location.hash;
  const current = pathname + window.location.search + window.location.hash;

  if (destination !== current) window.location.replace(destination);
})();`;

export function UnprefixedLocaleRedirect() {
  return (
    <Script id="unprefixed-locale-redirect" strategy="beforeInteractive">
      {localeEntryRedirectScript}
    </Script>
  );
}
