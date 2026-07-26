"use client";

import { useEffect } from "react";

const SECTION_SELECTOR = "[data-snap-section]";
const INTENT_THRESHOLD = 12;
const INPUT_RESET_MS = 180;
const SETTLE_LOCK_MS = 280;
const EDGE_TOLERANCE = 2;

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
    const desktopPointer = window.matchMedia(
      "(min-width: 1024px) and (pointer: fine)"
    );
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    let animationFrame = 0;
    let animating = false;
    let accumulatedIntent = 0;
    let lastInputAt = 0;
    let lockedUntil = 0;

    const isEnabled = () => desktopPointer.matches && !reducedMotion.matches;

    const cancelAnimation = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
      animationFrame = 0;
      animating = false;
      lockedUntil = 0;
      accumulatedIntent = 0;
    };

    const syncMode = () => {
      if (isEnabled()) {
        document.documentElement.dataset.snapScroll = "enabled";
      } else {
        delete document.documentElement.dataset.snapScroll;
        cancelAnimation();
      }
    };

    const sectionMetrics = () =>
      Array.from(document.querySelectorAll<HTMLElement>(SECTION_SELECTOR)).map(
        (section, index) => ({
          section,
          index,
          top: section.getBoundingClientRect().top + window.scrollY,
          height: section.offsetHeight,
          bottom:
            section.getBoundingClientRect().top +
            window.scrollY +
            section.offsetHeight
        })
      );

    const geometryTopFor = (target: HTMLElement) => {
      const snapSection = target.matches(SECTION_SELECTOR)
        ? target
        : target.closest<HTMLElement>(SECTION_SELECTOR) ??
          target.querySelector<HTMLElement>(SECTION_SELECTOR);

      const geometryTarget = snapSection ?? target;
      return geometryTarget.getBoundingClientRect().top + window.scrollY;
    };

    const currentSection = (
      sections: ReturnType<typeof sectionMetrics>
    ) => {
      const marker = window.scrollY + EDGE_TOLERANCE;
      let current = sections[0];

      for (const section of sections) {
        if (section.top > marker) {
          break;
        }
        current = section;
      }

      return current;
    };

    const animateTo = (
      requestedTop: number,
      source: "wheel" | "navigation" = "wheel"
    ) => {
      const maximumTop = Math.max(
        0,
        document.documentElement.scrollHeight - window.innerHeight
      );
      const startTop = window.scrollY;
      const targetTop = clamp(requestedTop, 0, maximumTop);
      const distance = Math.abs(targetTop - startTop);
      const duration =
        source === "navigation"
          ? clamp(880 + Math.sqrt(distance) * 15, 1100, 1700)
          : clamp(760 + Math.sqrt(distance) * 15, 900, 1500);
      const startedAt = performance.now();

      animating = true;
      accumulatedIntent = 0;

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
        animating = false;
        lockedUntil = performance.now() + SETTLE_LOCK_MS;
      };

      animationFrame = window.requestAnimationFrame(frame);
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

      if (animating || performance.now() < lockedUntil) {
        event.preventDefault();
        return;
      }

      const direction: 1 | -1 = delta > 0 ? 1 : -1;
      const sections = sectionMetrics();
      const current = currentSection(sections);
      if (!current) {
        return;
      }

      const isLongSection = current.height > window.innerHeight + EDGE_TOLERANCE;
      const distanceToEdge =
        direction > 0
          ? current.bottom - (window.scrollY + window.innerHeight)
          : window.scrollY - current.top;

      if (isLongSection && distanceToEdge > EDGE_TOLERANCE) {
        event.preventDefault();
        accumulatedIntent = 0;
        window.scrollBy(
          0,
          direction * Math.min(Math.abs(delta), Math.max(0, distanceToEdge))
        );
        return;
      }

      const target = sections[current.index + direction];
      if (!target) {
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

      animateTo(target.top);
    };

    const onAnchorClick = (event: MouseEvent) => {
      if (!isEnabled() || event.defaultPrevented || event.button !== 0) {
        return;
      }

      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }

      const clicked = event.target;
      if (!(clicked instanceof Element)) {
        return;
      }

      const anchor = clicked.closest<HTMLAnchorElement>("a[href^='#']");
      const hash = anchor?.getAttribute("href");
      if (!hash || hash === "#") {
        return;
      }

      const target = document.querySelector<HTMLElement>(hash);
      if (!target) {
        return;
      }

      event.preventDefault();
      cancelAnimation();
      window.history.pushState(null, "", hash);
      animateTo(geometryTopFor(target), "navigation");
    };

    const onKeyboardIntent = (event: KeyboardEvent) => {
      if (
        [
          "ArrowDown",
          "ArrowUp",
          "PageDown",
          "PageUp",
          "Home",
          "End",
          " "
        ].includes(event.key)
      ) {
        cancelAnimation();
      }
    };

    syncMode();
    desktopPointer.addEventListener("change", syncMode);
    reducedMotion.addEventListener("change", syncMode);
    window.addEventListener("wheel", onWheel, { passive: false });
    document.addEventListener("click", onAnchorClick);
    window.addEventListener("keydown", onKeyboardIntent);
    window.addEventListener("pointerdown", cancelAnimation);
    window.addEventListener("touchstart", cancelAnimation, { passive: true });

    return () => {
      cancelAnimation();
      delete document.documentElement.dataset.snapScroll;
      desktopPointer.removeEventListener("change", syncMode);
      reducedMotion.removeEventListener("change", syncMode);
      window.removeEventListener("wheel", onWheel);
      document.removeEventListener("click", onAnchorClick);
      window.removeEventListener("keydown", onKeyboardIntent);
      window.removeEventListener("pointerdown", cancelAnimation);
      window.removeEventListener("touchstart", cancelAnimation);
    };
  }, []);

  return null;
}
