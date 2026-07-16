"use client";

import { useLayoutEffect } from "react";

const targetSelector = "[data-text-reveal]";

export function SelectiveTextReveal() {
  useLayoutEffect(() => {
    const targets = Array.from(
      document.querySelectorAll<HTMLElement>(targetSelector)
    );
    if (!targets.length) return;

    const motionPreference = window.matchMedia(
      "(prefers-reduced-motion: reduce)"
    );
    if (motionPreference.matches || !("IntersectionObserver" in window)) {
      targets.forEach((target) => target.classList.add("isTextRevealed"));
      return;
    }

    document.documentElement.classList.add("textRevealReady");

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("isTextRevealed");
          observer.unobserve(entry.target);
        });
      },
      {
        threshold: 0.22,
        rootMargin: "0px 0px -10% 0px"
      }
    );

    let observeFrame = 0;
    const paintFrame = window.requestAnimationFrame(() => {
      observeFrame = window.requestAnimationFrame(() => {
        targets.forEach((target) => observer.observe(target));
      });
    });

    return () => {
      window.cancelAnimationFrame(paintFrame);
      window.cancelAnimationFrame(observeFrame);
      observer.disconnect();
    };
  }, []);

  return null;
}
