"use client";

import { localeDetails, localePath, type Locale, type LocalizedPage } from "@/data/site-copy";

export function LanguageSwitcher({
  locale,
  page,
  label
}: {
  locale: Locale;
  page: LocalizedPage;
  label: string;
}) {
  return (
    <label className="languageSelect">
      <span className="srOnly">{label}</span>
      <select
        value={locale}
        aria-label={label}
        onChange={(event) => {
          window.location.assign(localePath(event.target.value as Locale, page));
        }}
      >
        {(Object.keys(localeDetails) as Locale[]).map((option) => (
          <option key={option} value={option} lang={localeDetails[option].languageTag}>
            {localeDetails[option].label}
          </option>
        ))}
      </select>
      <svg viewBox="0 0 12 8" aria-hidden="true"><path d="m1 1 5 5 5-5" /></svg>
    </label>
  );
}
