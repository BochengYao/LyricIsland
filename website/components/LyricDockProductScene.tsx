"use client";

import { useEffect, useRef, useState } from "react";
import { ResponsiveImage } from "./ResponsiveImage";
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

    const timeout = window.setTimeout(() => setHasSettled(true), 1180);
    return () => window.clearTimeout(timeout);
  }, [hasEntered]);

  return (
    <figure
      ref={productSceneRef}
      className={`${styles.productScene} ${hasEntered && !hasSettled ? styles.productSceneEntered : ""} ${hasSettled ? styles.productSceneSettled : ""}`}
    >
      <ResponsiveImage
        fallbackSrc="/images/lyric-dock-product-scene.png"
        srcSet="/images/responsive/lyric-dock-product-scene-720.f900eac5.webp 720w, /images/responsive/lyric-dock-product-scene-1180.e8c7553d.webp 1180w, /images/responsive/lyric-dock-product-scene-2360.acd72915.webp 2360w, /images/responsive/lyric-dock-product-scene-3000.55f1618d.webp 3000w"
        placeholderSrc="/images/responsive/lyric-dock-product-scene-placeholder.8d85b206.webp"
        alt={alt}
        width={3000}
        height={383}
        sizes="(max-width: 767px) 100vw, (max-width: 1023px) 720px, (min-width: 1920px) 1928px, 100vw"
        className={styles.productSceneImage}
      />
    </figure>
  );
}
