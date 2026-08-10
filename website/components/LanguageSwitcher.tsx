"use client";

import { useEffect, useRef, useState } from "react";
import { localeDetails, localePath, type Locale, type LocalizedPage } from "@/data/site-copy";

const languageSwitcherLabels: Record<Locale, string> = {
  zh: "选择语言",
  zhHant: "選擇語言",
  en: "Choose language",
  ja: "言語を選択"
};

const locales = Object.keys(localeDetails) as Locale[];

export function LanguageSwitcher({
  locale,
  page
}: {
  locale: Locale;
  page: LocalizedPage;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const optionRefs = useRef<(HTMLAnchorElement | null)[]>([]);
  const current = localeDetails[locale];

  useEffect(() => {
    const closeWhenClickingElsewhere = (event: PointerEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setIsOpen(false);
    };

    document.addEventListener("pointerdown", closeWhenClickingElsewhere);
    return () => document.removeEventListener("pointerdown", closeWhenClickingElsewhere);
  }, []);

  const focusOption = (index: number) => {
    requestAnimationFrame(() => optionRefs.current[index]?.focus());
  };

  const openAndFocus = (index: number) => {
    setIsOpen(true);
    focusOption(index);
  };

  return (
    <div className="languageMenu" ref={menuRef}>
      <button
        ref={triggerRef}
        type="button"
        className="languageMenuTrigger"
        aria-label={languageSwitcherLabels[locale]}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        aria-controls="language-menu-options"
        onClick={() => setIsOpen((open) => !open)}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown") {
            event.preventDefault();
            openAndFocus(0);
          }
          if (event.key === "ArrowUp") {
            event.preventDefault();
            openAndFocus(locales.length - 1);
          }
        }}
      >
        <span lang={current.languageTag}>{current.label}</span>
        <svg viewBox="0 0 12 8" aria-hidden="true"><path d="m1 1 5 5 5-5" /></svg>
      </button>

      {isOpen ? (
        <div id="language-menu-options" className="languageMenuPanel" role="menu" aria-label={languageSwitcherLabels[locale]}>
          {locales.map((option, index) => {
            const detail = localeDetails[option];
            const isCurrent = option === locale;

            return (
              <a
                key={option}
                ref={(element) => { optionRefs.current[index] = element; }}
                href={localePath(option, page)}
                className="languageMenuOption"
                lang={detail.languageTag}
                role="menuitem"
                aria-current={isCurrent ? "page" : undefined}
                data-current={isCurrent || undefined}
                onClick={() => setIsOpen(false)}
                onKeyDown={(event) => {
                  if (event.key === "Escape") {
                    event.preventDefault();
                    setIsOpen(false);
                    triggerRef.current?.focus();
                  }
                  if (event.key === "ArrowDown") {
                    event.preventDefault();
                    focusOption((index + 1) % locales.length);
                  }
                  if (event.key === "ArrowUp") {
                    event.preventDefault();
                    focusOption((index - 1 + locales.length) % locales.length);
                  }
                  if (event.key === "Home") {
                    event.preventDefault();
                    focusOption(0);
                  }
                  if (event.key === "End") {
                    event.preventDefault();
                    focusOption(locales.length - 1);
                  }
                }}
              >
                <span>{detail.label}</span>
                {isCurrent ? (
                  <svg viewBox="0 0 16 12" aria-hidden="true"><path d="m1.5 6.5 4 4 9-9" /></svg>
                ) : null}
              </a>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
