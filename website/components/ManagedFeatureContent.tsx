"use client";

import { useEffect, useState } from "react";
import {
  localizedFeatureContent,
  sanitizeFeatureContent
} from "@/data/feature-content";
import type { FeatureContent } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";
import { preloadClientJson } from "@/lib/client-data";

type Props = {
  locale: Locale;
};

type FeatureResponse = { content?: unknown };

const featureContentPreload = preloadClientJson<FeatureResponse>("/api/features");

export function ManagedFeatureContent({ locale }: Props) {
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
    return (
      <section
        className={`databaseLoading updatesDatabaseLoading sectionContainer${loadFailed ? " isError" : ""}`}
        aria-busy={!loadFailed}
        role="status"
      >
        <span className="databaseLoadingLabel">
          {loadFailed
            ? locale === "zh" ? "更新内容暂时无法载入，请稍后刷新。" : "Updates could not be loaded. Please refresh later."
            : locale === "zh" ? "正在载入最新更新内容" : "Loading the latest updates"}
        </span>
        {!loadFailed && <span className="databaseLoadingPulse" aria-hidden="true" />}
      </section>
    );
  }

  const localized = localizedFeatureContent(content, locale);

  return (
    <>
      {localized.summaryVisible && (
        <section className="updatesSummary sectionContainer">
          <span>{localized.summaryLabel}</span>
          <ul>
            {localized.summary.map((item, index) => (
              <li key={`${index}-${item}`}>{item}</li>
            ))}
          </ul>
        </section>
      )}

      <section className="releaseSections sectionContainer">
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
      </section>
    </>
  );
}
