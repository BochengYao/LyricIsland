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
  heroSubtitle?: string;
  heroIntro: string;
  releaseLabel: string;
  versionPickerLabel: string;
  noPublishedVersions: string;
  releaseVersionUnavailable: string;
};

type FeatureResponse = { content?: unknown };

type VersionOption = {
  key: string;
  label: string;
  sections: FeatureContentSection[];
  parts: [number, number, number];
};

const featureContentPreload = preloadClientJson<FeatureResponse>("/api/features");
const releaseVersionPattern = /^v?(\d+)\.(\d+)\.(\d+)$/i;

function normalizeReleaseVersion(value: string) {
  const match = value.trim().match(releaseVersionPattern);
  if (!match) return null;

  const parts: [number, number, number] = [Number(match[1]), Number(match[2]), Number(match[3])];
  if (parts.some((part) => !Number.isSafeInteger(part))) return null;

  return {
    key: parts.join("."),
    label: `V${parts.join(".")}`,
    parts
  };
}

function compareVersionsNewestFirst(left: VersionOption, right: VersionOption) {
  for (let index = 0; index < left.parts.length; index += 1) {
    const difference = right.parts[index] - left.parts[index];
    if (difference) return difference;
  }
  return left.label.localeCompare(right.label, undefined, { numeric: true, sensitivity: "base" });
}

function availableVersions(content: FeatureContent) {
  const sectionsByVersion = new Map<string, FeatureContentSection[]>();

  for (const section of content.sections) {
    if (!section.visible) continue;
    const version = normalizeReleaseVersion(section.release_version);
    if (!version) continue;
    const sections = sectionsByVersion.get(version.key) ?? [];
    sections.push(section);
    sectionsByVersion.set(version.key, sections);
  }

  const options = new Map<string, VersionOption>();
  for (const rawVersion of content.versions) {
    const version = normalizeReleaseVersion(rawVersion);
    if (!version) continue;
    const sections = sectionsByVersion.get(version.key);
    if (!sections?.length || options.has(version.key)) continue;
    options.set(version.key, { ...version, sections });
  }

  return [...options.values()].sort(compareVersionsNewestFirst);
}

function VersionPicker({
  versions,
  selectedKey,
  label,
  onSelect
}: {
  versions: VersionOption[];
  selectedKey: string;
  label: string;
  onSelect: (key: string) => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const pickerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const optionRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const selectedIndex = Math.max(0, versions.findIndex((version) => version.key === selectedKey));
  const selected = versions[selectedIndex];

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

  const selectVersion = (key: string) => {
    onSelect(key);
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
            setIsOpen(true);
            focusOption(selectedIndex);
          }
        }}
      >
        <span className="versionPickerValue"><strong>{selected.label}</strong></span>
        <svg viewBox="0 0 12 8" aria-hidden="true"><path d="m1 1 5 5 5-5" /></svg>
      </button>

      {isOpen ? (
        <div id="release-version-options" className="versionPickerPanel" role="listbox" aria-label={label}>
          {versions.map((version, index) => {
            const isSelected = version.key === selectedKey;
            return (
              <button
                key={version.key}
                ref={(element) => { optionRefs.current[index] = element; }}
                type="button"
                className="versionPickerOption"
                role="option"
                aria-selected={isSelected}
                data-current={isSelected || undefined}
                onClick={() => selectVersion(version.key)}
                onKeyDown={(event) => {
                  if (event.key === "Escape") {
                    event.preventDefault();
                    setIsOpen(false);
                    triggerRef.current?.focus();
                  }
                  if (event.key === "ArrowDown") {
                    event.preventDefault();
                    focusOption((index + 1) % versions.length);
                  }
                  if (event.key === "ArrowUp") {
                    event.preventDefault();
                    focusOption((index - 1 + versions.length) % versions.length);
                  }
                  if (event.key === "Home") {
                    event.preventDefault();
                    focusOption(0);
                  }
                  if (event.key === "End") {
                    event.preventDefault();
                    focusOption(versions.length - 1);
                  }
                }}
              >
                <span>{version.label}</span>
                {isSelected ? <svg viewBox="0 0 16 12" aria-hidden="true"><path d="m1.5 6.5 4 4 9-9" /></svg> : null}
              </button>
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
  heroSubtitle,
  heroIntro,
  releaseLabel,
  versionPickerLabel,
  noPublishedVersions,
  releaseVersionUnavailable
}: Props) {
  const [content, setContent] = useState<FeatureContent | null>(null);
  const [selectedKey, setSelectedKey] = useState("");
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let active = true;
    const request = featureContentPreload ?? preloadClientJson<FeatureResponse>("/api/features");

    request?.then((data) => {
      if (!active || !data.content) return;
      const nextContent = sanitizeFeatureContent(data.content);
      const versions = availableVersions(nextContent);
      setContent(nextContent);
      setSelectedKey((current) => versions.some((version) => version.key === current) ? current : versions[0]?.key ?? "");
    }).catch(() => {
      if (active) setLoadFailed(true);
    });

    return () => { active = false; };
  }, []);

  if (!content) {
    const loadingText = loadFailed
      ? locale === "zh" ? "更新内容暂时无法载入，请稍后刷新。" : locale === "zhHant" ? "更新內容暫時無法載入，請稍後重新整理。" : locale === "ja" ? "更新内容を読み込めません。しばらくしてから再読み込みしてください。" : "Updates could not be loaded. Please refresh later."
      : locale === "zh" ? "正在载入" : locale === "zhHant" ? "正在載入" : locale === "ja" ? "読み込んでいます" : "Loading";

    return (
      <>
        <section className="updatesOverviewSnap" id="updates-overview" data-snap-section>
          <div className="updatesHero sectionContainer"><Eyebrow reveal>{heroEyebrow}</Eyebrow><h1 data-text-reveal="title">{heroTitle}</h1>{heroSubtitle ? <p className="updatesHeroSubtitle">{heroSubtitle}</p> : null}<p className="updatesLead">{heroIntro}</p></div>
          <div className={`databaseLoading updatesOverviewDatabaseLoading sectionContainer${loadFailed ? " isError" : ""}`} aria-busy={!loadFailed} role="status"><span className="databaseLoadingLabel">{loadingText}</span></div>
        </section>
        <section className="updatesDetailsSnap" id="updates-details" data-snap-section>
          <div className="releaseSections sectionContainer"><Eyebrow reveal>{releaseLabel}</Eyebrow><div className={`databaseLoading updatesDetailsDatabaseLoading${loadFailed ? " isError" : ""}`} aria-busy={!loadFailed} role="status"><span className="databaseLoadingLabel">{loadingText}</span></div></div>
        </section>
      </>
    );
  }

  const versions = availableVersions(content);
  const selected = versions.find((version) => version.key === selectedKey) ?? versions[0];
  const localizedSummary = localizedFeatureContent(content, locale);
  const localizedRelease = localizedFeatureContent({ ...content, sections: selected?.sections ?? [] }, locale);

  return (
    <>
      <section className="updatesOverviewSnap databaseContentReveal" id="updates-overview" data-snap-section>
        <div className="updatesHero sectionContainer"><Eyebrow reveal>{heroEyebrow}</Eyebrow><h1 data-text-reveal="title">{heroTitle}</h1>{heroSubtitle ? <p className="updatesHeroSubtitle">{heroSubtitle}</p> : null}<p className="updatesLead">{heroIntro}</p></div>
        {localizedSummary.summaryVisible ? <div className="updatesSummary sectionContainer"><span>{localizedSummary.summaryLabel}</span><ul>{localizedSummary.summary.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}</ul></div> : null}
      </section>

      <section className="updatesDetailsSnap databaseContentReveal" id="updates-details" data-snap-section>
        <div className="releaseSections sectionContainer">
          <div className="releaseHeader">
            <Eyebrow reveal>{selected?.label ?? releaseLabel}</Eyebrow>
            {selected ? <div className="updatesVersionPicker"><span className="updatesVersionPickerLabel">{versionPickerLabel}</span><VersionPicker versions={versions} selectedKey={selected.key} label={versionPickerLabel} onSelect={setSelectedKey} /></div> : null}
          </div>
          {selected ? localizedRelease.sections.map((section) => <article className="releaseSection" key={section.number}><span className="releaseNumber">{section.number}</span><div><h2>{section.title}</h2><p>{section.body}</p></div><ul>{section.items.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}</ul></article>) : <p className="updatesEmpty">{content.sections.some((section) => section.visible) ? releaseVersionUnavailable : noPublishedVersions}</p>}
        </div>
      </section>
    </>
  );
}
