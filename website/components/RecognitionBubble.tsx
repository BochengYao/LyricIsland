"use client";

import { useState } from "react";
import { ExternalArrow } from "@/components/ExternalArrow";

type Props = {
  label: string;
  summary: string;
  linkLabel: string;
  dismissLabel: string;
  href: string;
};

export function RecognitionBubble({
  label,
  summary,
  linkLabel,
  dismissLabel,
  href
}: Props) {
  const [visible, setVisible] = useState(true);

  if (!visible) return null;

  return (
    <aside className="recognitionBubble" aria-label={label}>
      <div className="recognitionBubbleCopy">
        <p className="recognitionBubbleLabel">{label}</p>
        <p className="recognitionBubbleSummary">{summary}</p>
      </div>
      <a
        className="recognitionBubbleLink"
        href={href}
        target="_blank"
        rel="noreferrer"
      >
        {linkLabel}
        <ExternalArrow />
      </a>
      <button
        className="recognitionBubbleClose"
        type="button"
        aria-label={dismissLabel}
        onClick={() => setVisible(false)}
      >
        <span aria-hidden="true">×</span>
      </button>
    </aside>
  );
}
