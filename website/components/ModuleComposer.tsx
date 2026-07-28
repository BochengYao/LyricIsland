"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";

const moduleIconPaths = [
  "M2,2 L16,2 L16,16 L2,16 Z M4,13 L8,8 L11,11 L13,9 L15,13 Z M12,5 A2,2 0 1 1 11.99,5 Z",
  "M2,3 L7,3 L7,8 L4,8 L4,11 L2,11 Z M10,3 L15,3 L15,8 L12,8 L12,11 L10,11 Z M2,14 L15,14 L15,16 L2,16 Z",
  "M3,2 L3,16 L13,9 Z M14,2 L16,2 L16,16 L14,16 Z",
  "M2,2 L16,2 L16,5 L2,5 Z M5,7 A2,2 0 1 1 4.99,7 Z M1,15 C1,12.5 3,11 5,11 C7,11 9,12.5 9,15 Z M11,9 L17,9 L17,11 L11,11 Z M11,13 L16,13 L16,15 L11,15 Z",
  "M2,8 L16,8 L16,10 L2,10 Z M9,6 A3,3 0 1 1 8.99,6 Z",
  "M8,1 L10,1 L10,17 L8,17 Z"
];

type Props = {
  label: string;
  names: string[];
  imageAlt: string;
};

export function ModuleComposer({ label, names, imageAlt }: Props) {
  const composerRef = useRef<HTMLDivElement>(null);
  const [contentVisible, setContentVisible] = useState(false);

  useEffect(() => {
    const composer = composerRef.current;
    const section = composer?.closest<HTMLElement>(".modulesSection");
    if (!composer || !section) {
      return;
    }

    let frame = 0;
    const update = () => {
      frame = 0;
      const rect = section.getBoundingClientRect();
      const scrollRange = Math.max(1, section.offsetHeight - window.innerHeight);
      const sectionProgress = Math.max(0, Math.min(1, -rect.top / scrollRange));
      const reveal = Math.max(
        0,
        Math.min(1, (sectionProgress - 0.16) / 0.56)
      );

      composer.style.setProperty("--module-reveal", reveal.toFixed(4));
      composer.style.setProperty(
        "--module-image-scale",
        (1.031 + reveal * 0.018).toFixed(4)
      );
      setContentVisible((current) => {
        const next = reveal >= 0.5;
        return current === next ? current : next;
      });
    };
    const requestUpdate = () => {
      if (!frame) {
        frame = window.requestAnimationFrame(update);
      }
    };

    update();
    window.addEventListener("scroll", requestUpdate, { passive: true });
    window.addEventListener("resize", requestUpdate);

    return () => {
      window.removeEventListener("scroll", requestUpdate);
      window.removeEventListener("resize", requestUpdate);
      if (frame) {
        window.cancelAnimationFrame(frame);
      }
    };
  }, []);

  return (
    <div
      ref={composerRef}
      className="moduleComposer"
      aria-label={label}
      data-content-visible={contentVisible ? "true" : "false"}
    >
      <div
        className="moduleComposerImage"
        aria-hidden={contentVisible ? "true" : undefined}
      >
        <Image
          src="/images/module-layout-intro.png"
          alt={imageAlt}
          fill
          sizes="(max-width: 1023px) calc(100vw - 64px), 58vw"
        />
      </div>
      <div
        className="moduleComposerContent"
        aria-hidden={contentVisible ? undefined : "true"}
      >
        <div className="moduleIsland">
          {names.slice(0, 4).map((name, index) => (
            <span className={"moduleBlock moduleBlock" + (index + 1)} key={name}>
              <i aria-hidden="true" />
              {name}
            </span>
          ))}
        </div>
        <div className="moduleTray">
          {names.map((name, index) => (
            <span key={name}>
              <svg
                className="moduleTrayIcon"
                viewBox="0 0 18 18"
                aria-hidden="true"
              >
                <path
                  d={moduleIconPaths[index]}
                  fillRule="evenodd"
                  clipRule="evenodd"
                />
              </svg>
              {name}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
