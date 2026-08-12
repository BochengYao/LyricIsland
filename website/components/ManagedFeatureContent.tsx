"use client";

import { useEffect, useRef, useState } from "react";
import { Eyebrow } from "@/components/SitePage";
import { localizedFeatureContent, sanitizeFeatureContent } from "@/data/feature-content";
import type { FeatureContent, FeatureContentSection } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";
import { preloadClientJson } from "@/lib/client-data";

type Props = {
  locale: Locale;
  heroEyebrow: string;
  heroTitle: string;
  heroIntro: string;
  releaseLabel: string;
  versionPickerLabel: string;
  noPublishedVersions: string;
  releaseVersionUnavailable: string;
};

type FeatureResponse = { content?: unknown };

type PublicFeatureContentSection = FeatureContentSection & {
  release_version?: string;
  major_version?: string;
};

type PublicFeatureContent = Omit<FeatureContent, "sections"> & {
  sections: PublicFeatureContentSection[];
};

type FeatureGroup = {
  majorVersion: string;
  sections: PublicFeatureContentSection[];
};

const featureContentPreload = preloadClientJson<FeatureResponse>("/api/features");

function sanitizePublicFeatureContent(value: unknown): PublicFeatureContent {
  const source = value && typeof value === "object" ? value as { sections?: unknown[] } : {};
  const releaseMetadata = new Map<string, { release_version?: string; major_version?: string }>();

  source.sections?.forEach((section, index) => {
    if (!section || typeof section !== "object") return;
    const raw = section as Record<string, unknown>;
    const id = typeof raw.id === "string" && raw.id.trim()
      ? raw.id.trim()
      : `feature-${String(index + 1).padStart(2, "0")}`;
    releaseMetadata.set(id, {
      release_version: typeof raw.release_version === "string" ? raw.release_version.trim() : undefined,
      major_version: typeof raw.major_version === "string" ? raw.major_version.trim() : undefined
    });
  });

  const content = sanitizeFeatureContent(value);
  return {
    ...content,
    sections: content.sections.map((section) => ({
      ...section,
      ...releaseMetadata.get(section.id)
    }))
  };
}

function groupFeatureSections(sections: PublicFeatureContentSection[]) {
  const groups = new Map<string, PublicFeatureContentSection[]>();

  for (const section of sections) {
    if (!section.visible) continue;
    if (!section.release_version?.trim()) continue;
    const majorVersion = section.major_version?.trim() || "OTHER";
    const current = groups.get(majorVersion) ?? [];
    current.push(section);
    groups.set(majorVersion, current);
  }

  return [...groups.entries()].map(([majorVersion, groupedSections]) => ({
    majorVersion,
    sections: groupedSections
  } satisfies FeatureGroup));
}

function VersionPicker({
  groups,
  selectedId,
  label,
  onSelect
}: {
  groups: FeatureGroup[];
  selectedId: string;
  label: string;
  onSelect: (id: string) => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const pickerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const optionRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const sections = groups.flatMap((group) => group.sections);
  const selectedIndex = Math.max(0, sections.findIndex((section) => section.id === selectedId));
  const selected = sections[selectedIndex];

  useEffect(() => {
    const closeWhenClickingElsewhere = (event: PointerEvent) => {
      if (!pickerRef.current?.contains(event.target as Node)) setIsOpen(false);
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

  const selectSection = (id: string) => {
    onSelect(id);
    setIsOpen(false);
    requestAnimationFrame(() => triggerRef.current?.focus());
  };

  return (
    <div className="versionPicker" ref={pickerRef}>
      <button
        ref={triggerRef}
        type="button"
        className="versionPickerTrigger"
        aria-label={label}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls="release-version-options"
        onClick={() => setIsOpen((open) => !open)}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            openAndFocus(selectedIndex);
          }
        }}
      >
        <span className="versionPickerValue">
          <strong>{selected?.release_version}</strong>
          <small>{selected?.major_version}</small>
        </span>
        <svg viewBox="0 0 12 8" aria-hidden="true"><path d="m1 1 5 5 5-5" /></svg>
      </button>

      {isOpen ? (
        <div id="release-version-options" className="versionPickerPanel" role="listbox" aria-label={label}>
          {groups.map((group, groupIndex) => {
            const optionStart = groups.slice(0, groupIndex).reduce((total, current) => total + current.sections.length, 0);

            return (
              <div className="versionPickerGroup" role="group" aria-label={group.majorVersion} key={group.majorVersion}>
                <span className="versionPickerGroupLabel">{group.majorVersion}</span>
                {group.sections.map((section, index) => {
                  const optionIndex = optionStart + index;
                  const isSelected = section.id === selectedId;

                  return (
                    <button
                      key={section.id}
                      ref={(element) => { optionRefs.current[optionIndex] = element; }}
                      type="button"
                      className="versionPickerOption"
                      role="option"
                      aria-selected={isSelected}
                      data-current={isSelected || undefined}
                      onClick={() => selectSection(section.id)}
                      onKeyDown={(event) => {
                        if (event.key === "Escape") {
                          event.preventDefault();
                          setIsOpen(false);
                          triggerRef.current?.focus();
                        }
                        if (event.key === "ArrowDown") {
                          event.preventDefault();
                          focusOption((optionIndex + 1) % sections.length);
                        }
                        if (event.key === "ArrowUp") {
                          event.preventDefault();
                          focusOption((optionIndex - 1 + sections.length) % sections.length);
                        }
                        if (event.key === "Home") {
                          event.preventDefault();
                          focusOption(0);
                        }
                        if (event.key === "End") {
                          event.preventDefault();
                          focusOption(sections.length - 1);
                        }
                      }}
                    >
                      <span>{section.release_version}</span>
                      {isSelected ? (
                        <svg viewBox="0 0 16 12" aria-hidden="true"><path d="m1.5 6.5 4 4 9-9" /></svg>
                      ) : null}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}

export function ManagedFeatureContent({
  locale,
  heroEyebrow,
  heroTitle,
  heroIntro,
  releaseLabel,
  versionPickerLabel,
  noPublishedVersions,
  releaseVersionUnavailable
}: Props) {
  const [content, setContent] = useState<PublicFeatureContent | null>(null);
  const [selectedId, setSelectedId] = useState("");
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let active = true;
    const request = featureContentPreload ?? preloadClientJson<FeatureResponse>("/api/features");

    request
      ?.then((data) => {
        if (!active || !data.content) return;
        const nextContent = sanitizePublicFeatureContent(data.content);
        const firstVisibleSection = nextContent.sections.find((section) => section.visible);
        setContent(nextContent);
        setSelectedId((current) => nextContent.sections.some((section) => section.id === current)
          ? current
          : firstVisibleSection?.id ?? "");
      })
      .catch(() => {
        if (active) setLoadFailed(true);
      });

    return () => {
      active = false;
    };
  }, []);

  if (!content) {
    const loadingText = loadFailed
      ? locale === "zh" ? "更新内容暂时无法载入，请稍后刷新。" : locale === "zhHant" ? "更新內容暫時無法載入，請稍後重新整理。" : locale === "ja" ? "更新内容を読み込めません。しばらくしてから再読み込みしてください。" : "Updates could not be loaded. Please refresh later."
      : locale === "zh" ? "正在载入" : locale === "zhHant" ? "正在載入" : locale === "ja" ? "読み込んでいます" : "Loading";

    return (
      <>
        <section className="updatesOverviewSnap" id="updates-overview" data-snap-section>
          <div className="updatesHero sectionContainer">
            <Eyebrow reveal>{heroEyebrow}</Eyebrow>
            <h1 data-text-reveal="title">{heroTitle}</h1>
            <p className="updatesLead">{heroIntro}</p>
          </div>
          <div className={`databaseLoading updatesOverviewDatabaseLoading sectionContainer${loadFailed ? " isError" : ""}`} aria-busy={!loadFailed} role="status">
            <span className="databaseLoadingLabel">{loadingText}</span>
          </div>
        </section>

        <section className="updatesDetailsSnap" id="updates-details" data-snap-section>
          <div className="releaseSections sectionContainer">
            <Eyebrow reveal>{releaseLabel}</Eyebrow>
            <div className={`databaseLoading updatesDetailsDatabaseLoading${loadFailed ? " isError" : ""}`} aria-busy={!loadFailed} role="status">
              <span className="databaseLoadingLabel">{loadingText}</span>
            </div>
          </div>
        </section>
      </>
    );
  }

  const groups = groupFeatureSections(content.sections);
  const selected = groups.flatMap((group) => group.sections).find((section) => section.id === selectedId) ?? groups[0]?.sections[0];
  const localized = localizedFeatureContent({ ...content, sections: selected ? [selected] : [] }, locale).sections[0];
  const noCompatibleVersion = content.sections.some((section) => section.visible) && groups.length === 0;

  return (
    <>
      <section className="updatesOverviewSnap databaseContentReveal" id="updates-overview" data-snap-section>
        <div className="updatesHero sectionContainer">
          <Eyebrow reveal>{heroEyebrow}</Eyebrow>
          <h1 data-text-reveal="title">{heroTitle}</h1>
          <p className="updatesLead">{heroIntro}</p>
        </div>

        {selected ? (
          <div className="updatesVersionPicker sectionContainer">
            <span className="updatesVersionPickerLabel">{versionPickerLabel}</span>
            <VersionPicker groups={groups} selectedId={selected.id} label={versionPickerLabel} onSelect={setSelectedId} />
          </div>
        ) : (
          <div className="databaseLoading updatesOverviewDatabaseLoading sectionContainer" role="status">
            <span className="databaseLoadingLabel">{noCompatibleVersion ? releaseVersionUnavailable : noPublishedVersions}</span>
          </div>
        )}
      </section>

      <section className="updatesDetailsSnap databaseContentReveal" id="updates-details" data-snap-section>
        <div className="releaseSections sectionContainer">
          <Eyebrow reveal>{selected?.major_version ?? releaseLabel}</Eyebrow>
          {localized && selected ? (
            <article className="releaseSection">
              <span className="releaseNumber">{selected.release_version}</span>
              <div>
                <h2>{localized.title}</h2>
                <p>{localized.body}</p>
              </div>
              {localized.items.length ? (
                <ul>
                  {localized.items.map((item, index) => (
                    <li key={`${index}-${item}`}>{item}</li>
                  ))}
                </ul>
              ) : null}
            </article>
          ) : (
            <p className="updatesEmpty">{noCompatibleVersion ? releaseVersionUnavailable : noPublishedVersions}</p>
          )}
        </div>
      </section>
    </>
  );
}
