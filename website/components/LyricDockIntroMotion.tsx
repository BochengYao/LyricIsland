"use client";

import { useLayoutEffect } from "react";

const selector = "#lyric-dock [data-lyric-dock-reveal]";

export function LyricDockIntroMotion() {
  useLayoutEffect(() => {
    const targets = Array.from(document.querySelectorAll<HTMLElement>(selector));
    if (!targets.length) return;

    const revealAll = () => {
      targets.forEach((target) => {
        target.dataset.lyricDockRevealed = "true";
      });
    };

    if (
      window.matchMedia("(prefers-reduced-motion: reduce)").matches ||
      !("IntersectionObserver" in window)
    ) {
      revealAll();
      return;
    }

    document.documentElement.dataset.lyricDockRevealReady = "true";

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          (entry.target as HTMLElement).dataset.lyricDockRevealed = "true";
          observer.unobserve(entry.target);
        });
      },
      {
        rootMargin: "0px 0px -10%",
        threshold: 0.22,
      },
    );

    const frame = window.requestAnimationFrame(() => {
      targets.forEach((target) => observer.observe(target));
    });

    return () => {
      window.cancelAnimationFrame(frame);
      observer.disconnect();
    };
  }, []);

  return null;
}
