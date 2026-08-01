"use client";

import { useEffect, useState } from "react";
import {
  defaultFeatureContent,
  localizedFeatureContent,
  sanitizeFeatureContent
} from "@/data/feature-content";
import type { FeatureContent } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";
import { Eyebrow } from "@/components/SitePage";

type Props = {
  locale: Locale;
  heroEyebrow: string;
  heroTitle: string;
  heroIntro: string;
  releaseLabel: string;
};

export function ManagedFeatureContent({
  locale,
  heroEyebrow,
  heroTitle,
  heroIntro,
  releaseLabel
}: Props) {
  const [content, setContent] = useState<FeatureContent>(defaultFeatureContent);

  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/features", { cache: "no-store", signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) return;
        const data = await response.json() as { content?: unknown };
        if (data.content) setContent(sanitizeFeatureContent(data.content));
      })
      .catch(() => {
        // The static bundled copy remains visible if the managed API is unavailable.
      });
    return () => controller.abort();
  }, []);

  const localized = localizedFeatureContent(content, locale);

  return (
    <>
      <section
        className="updatesOverviewSnap"
        id="updates-overview"
        data-snap-section
      >
        <div className="updatesHero sectionContainer">
          <Eyebrow reveal>{heroEyebrow}</Eyebrow>
          <h1 data-text-reveal="title">{heroTitle}</h1>
          <p className="updatesLead">{heroIntro}</p>
        </div>

        {localized.summaryVisible && (
          <div className="updatesSummary sectionContainer">
            <span>{localized.summaryLabel}</span>
            <ul>
              {localized.summary.map((item, index) => (
                <li key={`${index}-${item}`}>{item}</li>
              ))}
            </ul>
          </div>
        )}
      </section>

      <section
        className="updatesDetailsSnap"
        id="updates-details"
        data-snap-section
      >
        <div className="releaseSections sectionContainer">
          <Eyebrow reveal>{releaseLabel}</Eyebrow>
          {localized.sections.map((section) => (
            <article className="releaseSection" key={section.number}>
              <span className="releaseNumber">{section.number}</span>
              <div>
                <h2>{section.title}</h2>
                <p>{section.body}</p>
              </div>
              <ul>
                {section.items.map((item, index) => (
                  <li key={`${index}-${item}`}>{item}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>
    </>
  );
}
