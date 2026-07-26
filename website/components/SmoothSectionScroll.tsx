"use client";

import { useEffect } from "react";

const SECTION_SELECTOR = "[data-snap-section]";
const INTENT_THRESHOLD = 36;
const INPUT_RESET_MS = 180;
const RELEASE_LOCK_MS = 1100;
const EDGE_TOLERANCE = 4;

function wheelDistance(event: WheelEvent) {
  if (event.deltaMode === WheelEvent.DOM_DELTA_LINE) {
    return event.deltaY * 16;
  }

  if (event.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
    return event.deltaY * window.innerHeight;
  }

  return event.deltaY;
}

export function SmoothSectionScroll() {
  useEffect(() => {
    const desktopViewport = window.matchMedia("(min-width: 1024px)");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    let accumulatedIntent = 0;
    let lastInputAt = 0;
    let locked = false;
    let releaseTimer = 0;

    const isEnabled = () => desktopViewport.matches && !reducedMotion.matches;

    const releaseLock = () => {
      window.clearTimeout(releaseTimer);
      releaseTimer = 0;
      locked = false;
      accumulatedIntent = 0;
    };

    const syncMode = () => {
      if (isEnabled()) {
        document.documentElement.dataset.snapScroll = "enabled";
      } else {
        delete document.documentElement.dataset.snapScroll;
        releaseLock();
      }
    };

    const sectionTops = () =>
      Array.from(document.querySelectorAll<HTMLElement>(SECTION_SELECTOR)).map(
        (section) => section.getBoundingClientRect().top + window.scrollY
      );

    const targetTopFor = (direction: 1 | -1) => {
      const currentTop = window.scrollY;
      const tops = sectionTops();

      if (direction > 0) {
        return tops.find((top) => top > currentTop + EDGE_TOLERANCE);
      }

      return tops
        .slice()
        .reverse()
        .find((top) => top < currentTop - EDGE_TOLERANCE);
    };

    const onWheel = (event: WheelEvent) => {
      if (!isEnabled() || event.ctrlKey) {
        return;
      }

      if (
        event.target instanceof Element &&
        event.target.closest("[data-native-scroll], dialog, [role='dialog']")
      ) {
        return;
      }

      const delta = wheelDistance(event);
      if (Math.abs(delta) < 1) {
        return;
      }

      if (locked) {
        event.preventDefault();
        return;
      }

      const direction: 1 | -1 = delta > 0 ? 1 : -1;
      const targetTop = targetTopFor(direction);
      if (targetTop === undefined) {
        return;
      }

      event.preventDefault();
      const now = performance.now();
      if (now - lastInputAt > INPUT_RESET_MS) {
        accumulatedIntent = 0;
      }
      lastInputAt = now;
      accumulatedIntent += delta;

      if (Math.abs(accumulatedIntent) < INTENT_THRESHOLD) {
        return;
      }

      locked = true;
      accumulatedIntent = 0;
      window.scrollTo({ top: targetTop, behavior: "smooth" });
      releaseTimer = window.setTimeout(releaseLock, RELEASE_LOCK_MS);
    };

    syncMode();
    desktopViewport.addEventListener("change", syncMode);
    reducedMotion.addEventListener("change", syncMode);
    window.addEventListener("wheel", onWheel, { passive: false });
    window.addEventListener("scrollend", releaseLock);
    window.addEventListener("pointerdown", releaseLock);
    window.addEventListener("keydown", releaseLock);

    return () => {
      releaseLock();
      delete document.documentElement.dataset.snapScroll;
      desktopViewport.removeEventListener("change", syncMode);
      reducedMotion.removeEventListener("change", syncMode);
      window.removeEventListener("wheel", onWheel);
      window.removeEventListener("scrollend", releaseLock);
      window.removeEventListener("pointerdown", releaseLock);
      window.removeEventListener("keydown", releaseLock);
    };
  }, []);

  return null;
}
