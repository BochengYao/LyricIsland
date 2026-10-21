"use client";

import { useEffect, useRef, useState, type CSSProperties } from "react";

type Props = {
  alt: string;
  className?: string;
  fallbackSrc: string;
  fetchPriority?: "high" | "low" | "auto";
  fill?: boolean;
  height: number;
  loading?: "eager" | "lazy";
  placeholderPosition?: string;
  placeholderSrc: string;
  sizes: string;
  srcSet: string;
  style?: CSSProperties;
  width: number;
};

export function ResponsiveImage({
  alt,
  className,
  fallbackSrc,
  fetchPriority = "auto",
  fill = false,
  height,
  loading = "lazy",
  placeholderPosition = "center",
  placeholderSrc,
  sizes,
  srcSet,
  style,
  width,
}: Props) {
  const [loaded, setLoaded] = useState(false);
  const imageRef = useRef<HTMLImageElement>(null);
  const placeholderStyle = {
    "--responsive-image-placeholder": `url("${placeholderSrc}")`,
    "--responsive-image-position": placeholderPosition,
  } as CSSProperties;

  useEffect(() => {
    const image = imageRef.current;
    if (image?.complete && image.naturalWidth > 0) {
      setLoaded(true);
    }
  }, []);

  return (
    <picture
      className={`responsivePicture${fill ? " responsivePictureFill" : ""}`}
      data-loaded={loaded ? "true" : "false"}
      style={placeholderStyle}
    >
      <source type="image/webp" srcSet={srcSet} sizes={sizes} />
      <img
        ref={imageRef}
        src={fallbackSrc}
        alt={alt}
        width={width}
        height={height}
        sizes={sizes}
        loading={loading}
        fetchPriority={fetchPriority}
        decoding="async"
        className={className}
        style={style}
        onLoad={() => setLoaded(true)}
      />
    </picture>
  );
}
