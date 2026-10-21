"use client";

import { useEffect } from "react";

const SECTION_SELECTOR = "[data-snap-section]";
const INTENT_THRESHOLD = 12;
const INPUT_RESET_MS = 180;
const SETTLE_LOCK_MS = 280;
const NATIVE_SCROLL_SETTLE_MS = 180;
const EDGE_TOLERANCE = 2;
const TOUCH_INTENT_THRESHOLD = 36;

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
    const mobilePointer = window.matchMedia(
      "(max-width: 1023px), (pointer: coarse)"
    );
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    let animationFrame = 0;
    let animating = false;
    let accumulatedIntent = 0;
    let lastInputAt = 0;
    let lockedUntil = 0;
    let scrollSettleTimer = 0;
    let pointerActive = false;
    let touchStartY = 0;
    let touchCurrentY = 0;
    let touchMode: "idle" | "native" | "snap" = "idle";

    const isEnabled = () => desktopPointer.matches && !reducedMotion.matches;
    const isMobileEnabled = () => mobilePointer.matches;
    const isSnapEnabled = () => isEnabled() || isMobileEnabled();

    const snapTopInset = () => {
      if (!isMobileEnabled()) {
        return 0;
      }

      const navigation = document.querySelector<HTMLElement>(".floatingNav");
      return navigation
        ? Math.ceil(navigation.getBoundingClientRect().bottom + 12)
        : 88;
    };

    const cancelAnimation = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
      if (scrollSettleTimer) {
        window.clearTimeout(scrollSettleTimer);
      }
      animationFrame = 0;
      scrollSettleTimer = 0;
      animating = false;
      lockedUntil = 0;
      accumulatedIntent = 0;
    };

    const syncMode = () => {
      if (isSnapEnabled()) {
        document.documentElement.dataset.snapScroll = "enabled";
      } else {
        delete document.documentElement.dataset.snapScroll;
        cancelAnimation();
      }
    };

    const sectionMetrics = () =>
      Array.from(document.querySelectorAll<HTMLElement>(SECTION_SELECTOR))
        .filter((section) => section.offsetHeight > EDGE_TOLERANCE)
        .map((section, index) => ({
          section,
          index,
          top: section.getBoundingClientRect().top + window.scrollY,
          height: section.offsetHeight,
          bottom:
            section.getBoundingClientRect().top +
            window.scrollY +
            section.offsetHeight
        }));

    const geometryTopFor = (target: HTMLElement) => {
      const snapSection = target.matches(SECTION_SELECTOR)
        ? target
        : target.closest<HTMLElement>(SECTION_SELECTOR) ??
          target.querySelector<HTMLElement>(SECTION_SELECTOR);

      const geometryTarget = snapSection ?? target;
      return (
        geometryTarget.getBoundingClientRect().top +
        window.scrollY -
        snapTopInset()
      );
    };

    const currentSection = (
      sections: ReturnType<typeof sectionMetrics>
    ) => {
      const marker = window.scrollY + snapTopInset() + EDGE_TOLERANCE;
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
      if (reducedMotion.matches) {
        window.scrollTo(0, targetTop);
        return;
      }
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

    const settleNativeScroll = () => {
      if (!isEnabled() || animating || pointerActive) {
        return;
      }

      if (scrollSettleTimer) {
        window.clearTimeout(scrollSettleTimer);
      }

      scrollSettleTimer = window.setTimeout(() => {
        scrollSettleTimer = 0;
        if (!isEnabled() || animating || pointerActive) {
          return;
        }

        const sections = sectionMetrics();
        const current = currentSection(sections);
        const directSnap = current?.section.dataset.wheelSnap === "direct";
        if (
          !current ||
          (current.height > window.innerHeight + EDGE_TOLERANCE && !directSnap)
        ) {
          return;
        }

        const offsetWithinSection = window.scrollY - current.top;
        if (Math.abs(offsetWithinSection) <= EDGE_TOLERANCE) {
          return;
        }

        const next = sections[current.index + 1];
        const target =
          next && offsetWithinSection > current.height / 2 ? next : current;
        animateTo(target.top);
      }, NATIVE_SCROLL_SETTLE_MS);
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

      const directSnap = current.section.dataset.wheelSnap === "direct";
      const nativeWheelScroll =
        current.section.dataset.wheelScroll === "native";
      const isLongSection =
        current.height > window.innerHeight + EDGE_TOLERANCE && !directSnap;
      const distanceToEdge =
        direction > 0
          ? current.bottom - (window.scrollY + window.innerHeight)
          : window.scrollY - current.top;

      if (
        isLongSection &&
        current.section.dataset.stagedScroll === "true" &&
        distanceToEdge > EDGE_TOLERANCE
      ) {
        event.preventDefault();
        accumulatedIntent = 0;
        animateTo(
          direction > 0
            ? current.bottom - window.innerHeight
            : current.top
        );
        return;
      }

      if (
        isLongSection &&
        nativeWheelScroll &&
        distanceToEdge > EDGE_TOLERANCE
      ) {
        accumulatedIntent = 0;
        return;
      }

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

      const targetTop =
        direction < 0 && target.height > window.innerHeight + EDGE_TOLERANCE
          ? target.bottom - window.innerHeight
          : target.top - snapTopInset();

      animateTo(targetTop);
    };

    const onAnchorClick = (event: MouseEvent) => {
      if (!isSnapEnabled() || event.defaultPrevented || event.button !== 0) {
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

    const onPointerDown = () => {
      pointerActive = true;
      cancelAnimation();
    };

    const onPointerUp = () => {
      pointerActive = false;
      settleNativeScroll();
    };

    const onTouchStart = (event: TouchEvent) => {
      if (!isMobileEnabled() || event.touches.length !== 1) {
        return;
      }

      cancelAnimation();
      touchStartY = event.touches[0].clientY;
      touchCurrentY = touchStartY;
      touchMode = "idle";
    };

    const onTouchMove = (event: TouchEvent) => {
      if (!isMobileEnabled() || event.touches.length !== 1) {
        return;
      }

      if (
        event.target instanceof Element &&
        event.target.closest("[data-native-scroll], dialog, [role='dialog']")
      ) {
        touchMode = "native";
        return;
      }

      touchCurrentY = event.touches[0].clientY;
      const delta = touchStartY - touchCurrentY;
      if (Math.abs(delta) < EDGE_TOLERANCE) {
        return;
      }

      const direction: 1 | -1 = delta > 0 ? 1 : -1;
      const sections = sectionMetrics();
      const current = currentSection(sections);
      if (!current) {
        return;
      }

      const directSnap = current.section.dataset.wheelSnap === "direct";
      const nativeTouchScroll =
        current.section.dataset.wheelScroll === "native";
      const isLongSection =
        current.height > window.innerHeight + EDGE_TOLERANCE && !directSnap;
      const distanceToEdge =
        direction > 0
          ? current.bottom - (window.scrollY + window.innerHeight)
          : window.scrollY - current.top;

      if (
        isLongSection &&
        nativeTouchScroll &&
        distanceToEdge > EDGE_TOLERANCE
      ) {
        touchMode = "native";
        return;
      }

      touchMode = "snap";
      event.preventDefault();
    };

    const finishTouch = () => {
      if (!isMobileEnabled() || touchMode !== "snap") {
        touchMode = "idle";
        return;
      }

      const delta = touchStartY - touchCurrentY;
      touchMode = "idle";
      if (Math.abs(delta) < TOUCH_INTENT_THRESHOLD) {
        return;
      }

      const direction: 1 | -1 = delta > 0 ? 1 : -1;
      const sections = sectionMetrics();
      const current = currentSection(sections);
      const target = current && sections[current.index + direction];
      if (!target) {
        return;
      }

      const targetTop =
        direction < 0 && target.height > window.innerHeight + EDGE_TOLERANCE
          ? target.bottom - window.innerHeight
          : target.top;
      animateTo(targetTop);
    };

    syncMode();
    desktopPointer.addEventListener("change", syncMode);
    mobilePointer.addEventListener("change", syncMode);
    reducedMotion.addEventListener("change", syncMode);
    window.addEventListener("wheel", onWheel, { passive: false });
    window.addEventListener("scroll", settleNativeScroll, { passive: true });
    document.addEventListener("click", onAnchorClick);
    window.addEventListener("keydown", onKeyboardIntent);
    window.addEventListener("pointerdown", onPointerDown);
    window.addEventListener("pointerup", onPointerUp);
    window.addEventListener("pointercancel", onPointerUp);
    window.addEventListener("touchstart", onTouchStart, { passive: true });
    window.addEventListener("touchmove", onTouchMove, { passive: false });
    window.addEventListener("touchend", finishTouch, { passive: true });
    window.addEventListener("touchcancel", finishTouch, { passive: true });

    return () => {
      cancelAnimation();
      delete document.documentElement.dataset.snapScroll;
      desktopPointer.removeEventListener("change", syncMode);
      mobilePointer.removeEventListener("change", syncMode);
      reducedMotion.removeEventListener("change", syncMode);
      window.removeEventListener("wheel", onWheel);
      window.removeEventListener("scroll", settleNativeScroll);
      document.removeEventListener("click", onAnchorClick);
      window.removeEventListener("keydown", onKeyboardIntent);
      window.removeEventListener("pointerdown", onPointerDown);
      window.removeEventListener("pointerup", onPointerUp);
      window.removeEventListener("pointercancel", onPointerUp);
      window.removeEventListener("touchstart", onTouchStart);
      window.removeEventListener("touchmove", onTouchMove);
      window.removeEventListener("touchend", finishTouch);
      window.removeEventListener("touchcancel", finishTouch);
    };
  }, []);

  return null;
}
