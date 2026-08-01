"use client";

import { useEffect, useState } from "react";
import {
  localizedFeatureContent,
  sanitizeFeatureContent
} from "@/data/feature-content";
import type { FeatureContent } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";
import { Eyebrow } from "@/components/SitePage";
import { preloadClientJson } from "@/lib/client-data";

type Props = {
  locale: Locale;
  heroEyebrow: string;
  heroTitle: string;
  heroIntro: string;
  releaseLabel: string;
};

type FeatureResponse = { content?: unknown };

const featureContentPreload = preloadClientJson<FeatureResponse>("/api/features");

export function ManagedFeatureContent({
  locale,
  heroEyebrow,
  heroTitle,
  heroIntro,
  releaseLabel
}: Props) {
  const [content, setContent] = useState<FeatureContent | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let active = true;
    const request = featureContentPreload ?? preloadClientJson<FeatureResponse>("/api/features");
    request
      ?.then((data) => {
        if (active && data.content) setContent(sanitizeFeatureContent(data.content));
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
      ? locale === "zh" ? "更新内容暂时无法载入，请稍后刷新。" : "Updates could not be loaded. Please refresh later."
      : locale === "zh" ? "正在载入最新更新内容" : "Loading the latest updates";
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
          <div
            className={`databaseLoading updatesOverviewDatabaseLoading sectionContainer${loadFailed ? " isError" : ""}`}
            aria-busy={!loadFailed}
            role="status"
          >
            <span className="databaseLoadingLabel">{loadingText}</span>
            {!loadFailed && <span className="databaseLoadingPulse" aria-hidden="true" />}
          </div>
        </section>

        <section
          className="updatesDetailsSnap"
          id="updates-details"
          data-snap-section
        >
          <div className="releaseSections sectionContainer">
            <Eyebrow reveal>{releaseLabel}</Eyebrow>
            <div
              className={`databaseLoading updatesDetailsDatabaseLoading${loadFailed ? " isError" : ""}`}
              aria-busy={!loadFailed}
              role="status"
            >
              <span className="databaseLoadingLabel">{loadingText}</span>
              {!loadFailed && <span className="databaseLoadingPulse" aria-hidden="true" />}
            </div>
          </div>
        </section>
      </>
    );
  }

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
