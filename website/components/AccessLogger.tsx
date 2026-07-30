"use client";

import { usePathname } from "next/navigation";
import { useEffect, useRef } from "react";

export function AccessLogger() {
  const pathname = usePathname();
  const previousPath = useRef("");

  useEffect(() => {
    if (!pathname || previousPath.current === pathname) return;
    previousPath.current = pathname;
    const extendedNavigator = navigator as Navigator & {
      connection?: {
        effectiveType?: string;
        downlink?: number;
        rtt?: number;
        saveData?: boolean;
      };
      deviceMemory?: number;
      userAgentData?: { platform?: string; mobile?: boolean };
    };
    const connection = extendedNavigator.connection;
    const navigation = performance.getEntriesByType("navigation")[0] as PerformanceNavigationTiming | undefined;
    void fetch("/api/access", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        path: `${pathname}${window.location.search}`,
        referrer: document.referrer,
        details: {
          page_title: document.title,
          page_url: `${window.location.origin}${window.location.pathname}`,
          timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
          language: navigator.language,
          languages: navigator.languages?.slice(0, 12),
          platform: extendedNavigator.userAgentData?.platform || navigator.platform,
          mobile: extendedNavigator.userAgentData?.mobile ?? /Android|iPhone|iPad|Mobile/i.test(navigator.userAgent),
          viewport: `${window.innerWidth}×${window.innerHeight}`,
          screen: `${window.screen.width}×${window.screen.height}`,
          pixel_ratio: window.devicePixelRatio,
          color_depth: window.screen.colorDepth,
          touch_points: navigator.maxTouchPoints,
          hardware_concurrency: navigator.hardwareConcurrency,
          device_memory_gb: extendedNavigator.deviceMemory ?? null,
          cookies_enabled: navigator.cookieEnabled,
          do_not_track: navigator.doNotTrack,
          connection: connection ? {
            effective_type: connection.effectiveType ?? null,
            downlink_mbps: connection.downlink ?? null,
            rtt_ms: connection.rtt ?? null,
            save_data: connection.saveData ?? null
          } : null,
          navigation_type: navigation?.type ?? null
        }
      }),
      keepalive: true
    }).catch(() => undefined);
  }, [pathname]);

  return null;
}
