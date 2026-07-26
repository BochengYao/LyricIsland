"use client";

import { useEffect } from "react";

const SECTION_SELECTOR = "[data-snap-section]";
const INTENT_THRESHOLD = 36;
const INPUT_RESET_MS = 180;
const EXTRA_DURATION_MS = 300;
const EDGE_TOLERANCE = 4;

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function easeInOutQuint(progress: number) {
  return progress < 0.5
    ? 16 * progress ** 5
    : 1 - (-2 * progress + 2) ** 5 / 2;
}

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
    let animationFrame = 0;
    let releaseTimer = 0;

    const isEnabled = () => desktopViewport.matches && !reducedMotion.matches;

    const releaseLock = () => {
      window.clearTimeout(releaseTimer);
      releaseTimer = 0;
      locked = false;
      accumulatedIntent = 0;
      delete document.documentElement.dataset.snapAnimating;
    };

    const cancelAnimation = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
      animationFrame = 0;
      releaseLock();
    };

    const syncMode = () => {
      if (isEnabled()) {
        document.documentElement.dataset.snapScroll = "enabled";
      } else {
        delete document.documentElement.dataset.snapScroll;
        cancelAnimation();
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

    const animateTo = (requestedTop: number) => {
      const maximumTop = Math.max(
        0,
        document.documentElement.scrollHeight - window.innerHeight
      );
      const startTop = window.scrollY;
      const targetTop = clamp(requestedTop, 0, maximumTop);
      const distance = Math.abs(targetTop - startTop);
      const baseDuration = clamp(
        760 + Math.sqrt(distance) * 15,
        900,
        1500
      );
      const duration = baseDuration + EXTRA_DURATION_MS;
      const startedAt = performance.now();

      locked = true;
      document.documentElement.dataset.snapAnimating = "enabled";

      const frame = (now: number) => {
        const progress = clamp((now - startedAt) / duration, 0, 1);
        const eased = easeInOutQuint(progress);
        window.scrollTo(0, startTop + (targetTop - startTop) * eased);

        if (progress < 1) {
          animationFrame = window.requestAnimationFrame(frame);
          return;
        }

        window.scrollTo(0, targetTop);
        animationFrame = 0;
        releaseLock();
      };

      animationFrame = window.requestAnimationFrame(frame);
      releaseTimer = window.setTimeout(releaseLock, duration + 200);
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

      accumulatedIntent = 0;
      animateTo(targetTop);
    };

    syncMode();
    desktopViewport.addEventListener("change", syncMode);
    reducedMotion.addEventListener("change", syncMode);
    window.addEventListener("wheel", onWheel, { passive: false });
    window.addEventListener("pointerdown", cancelAnimation);
    window.addEventListener("keydown", cancelAnimation);

    return () => {
      cancelAnimation();
      delete document.documentElement.dataset.snapScroll;
      desktopViewport.removeEventListener("change", syncMode);
      reducedMotion.removeEventListener("change", syncMode);
      window.removeEventListener("wheel", onWheel);
      window.removeEventListener("pointerdown", cancelAnimation);
      window.removeEventListener("keydown", cancelAnimation);
    };
  }, []);

  return null;
}
