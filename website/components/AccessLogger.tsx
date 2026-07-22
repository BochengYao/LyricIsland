"use client";

import { usePathname } from "next/navigation";
import { useEffect, useRef } from "react";

export function AccessLogger() {
  const pathname = usePathname();
  const previousPath = useRef("");

  useEffect(() => {
    if (!pathname || previousPath.current === pathname) return;
    previousPath.current = pathname;
    void fetch("/api/access", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ path: pathname, referrer: document.referrer }),
      keepalive: true
    }).catch(() => undefined);
  }, [pathname]);

  return null;
}
