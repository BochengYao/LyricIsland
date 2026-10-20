"use client";

import { useEffect } from "react";

const SECTION_SELECTOR = "[data-snap-section]";

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function easeInOutQuint(progress: number) {
  return progress < 0.5
    ? 16 * progress ** 5
    : 1 - (-2 * progress + 2) ** 5 / 2;
}

export function SmoothSectionScroll() {
  useEffect(() => {
    const desktopPointer = window.matchMedia(
      "(min-width: 1024px) and (pointer: fine)"
    );
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    let animationFrame = 0;

    const isEnabled = () => desktopPointer.matches && !reducedMotion.matches;

    const cancelAnimation = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
      animationFrame = 0;
    };

    const syncMode = () => {
      if (isEnabled()) {
        document.documentElement.dataset.snapScroll = "enabled";
      } else {
        delete document.documentElement.dataset.snapScroll;
        cancelAnimation();
      }
    };

    const geometryTopFor = (target: HTMLElement) => {
      const snapSection = target.matches(SECTION_SELECTOR)
        ? target
        : target.closest<HTMLElement>(SECTION_SELECTOR) ??
          target.querySelector<HTMLElement>(SECTION_SELECTOR);

      const geometryTarget = snapSection ?? target;
      return geometryTarget.getBoundingClientRect().top + window.scrollY;
    };

    const animateTo = (requestedTop: number) => {
      const maximumTop = Math.max(
        0,
        document.documentElement.scrollHeight - window.innerHeight
      );
      const startTop = window.scrollY;
      const targetTop = clamp(requestedTop, 0, maximumTop);
      const distance = Math.abs(targetTop - startTop);
      const duration = clamp(880 + Math.sqrt(distance) * 15, 1100, 1700);
      const startedAt = performance.now();

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
      };

      animationFrame = window.requestAnimationFrame(frame);
    };

    const onAnchorClick = (event: MouseEvent) => {
      if (!isEnabled() || event.defaultPrevented || event.button !== 0) return;
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const clicked = event.target;
      if (!(clicked instanceof Element)) return;

      const anchor = clicked.closest<HTMLAnchorElement>("a[href^='#']");
      const hash = anchor?.getAttribute("href");
      if (!hash || hash === "#") return;

      const target = document.querySelector<HTMLElement>(hash);
      if (!target) return;

      event.preventDefault();
      cancelAnimation();
      window.history.pushState(null, "", hash);
      animateTo(geometryTopFor(target));
    };

    const onKeyboardIntent = (event: KeyboardEvent) => {
      if (["ArrowDown", "ArrowUp", "PageDown", "PageUp", "Home", "End", " "].includes(event.key)) {
        cancelAnimation();
      }
    };

    syncMode();
    desktopPointer.addEventListener("change", syncMode);
    reducedMotion.addEventListener("change", syncMode);
    window.addEventListener("wheel", cancelAnimation, { passive: true });
    document.addEventListener("click", onAnchorClick);
    window.addEventListener("keydown", onKeyboardIntent);
    window.addEventListener("pointerdown", cancelAnimation);
    window.addEventListener("touchstart", cancelAnimation, { passive: true });

    return () => {
      cancelAnimation();
      delete document.documentElement.dataset.snapScroll;
      desktopPointer.removeEventListener("change", syncMode);
      reducedMotion.removeEventListener("change", syncMode);
      window.removeEventListener("wheel", cancelAnimation);
      document.removeEventListener("click", onAnchorClick);
      window.removeEventListener("keydown", onKeyboardIntent);
      window.removeEventListener("pointerdown", cancelAnimation);
      window.removeEventListener("touchstart", cancelAnimation);
    };
  }, []);

  return null;
}
