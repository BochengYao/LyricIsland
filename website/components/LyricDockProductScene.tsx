"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import styles from "./LyricDockShowcase.module.css";

type Props = {
  alt: string;
};

export function LyricDockProductScene({ alt }: Props) {
  const productSceneRef = useRef<HTMLElement>(null);
  const [hasEntered, setHasEntered] = useState(false);
  const [hasSettled, setHasSettled] = useState(false);

  useEffect(() => {
    const productScene = productSceneRef.current;

    if (!productScene || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setHasEntered(true);
      return;
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return;

        setHasEntered(true);
        observer.disconnect();
      },
      {
        rootMargin: "0px 0px -8%",
        threshold: 0.18,
      },
    );

    observer.observe(productScene);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!hasEntered) return;

    const timeout = window.setTimeout(() => setHasSettled(true), 960);
    return () => window.clearTimeout(timeout);
  }, [hasEntered]);

  return (
    <figure
      ref={productSceneRef}
      className={`${styles.productScene} ${hasEntered && !hasSettled ? styles.productSceneEntered : ""} ${hasSettled ? styles.productSceneSettled : ""}`}
    >
      <Image
        src="/images/lyric-dock-product-scene.png"
        alt={alt}
        width={3000}
        height={383}
        sizes="(max-width: 767px) 390px, (max-width: 1023px) 720px, 1180px"
        loading="eager"
        unoptimized
        className={styles.productSceneImage}
      />
    </figure>
  );
}
