import type { Locale } from "@/data/site-copy";

function progressLabel(locale: Locale, progress: number | null) {
  if (progress === null) {
    if (locale === "zh") return "开发进度未知";
    if (locale === "zhHant") return "開發進度未知";
    if (locale === "ja") return "開発進捗は未設定です";
    return "Development progress unknown";
  }

  if (locale === "zh") return `开发进度 ${progress}%`;
  if (locale === "zhHant") return `開發進度 ${progress}%`;
  if (locale === "ja") return `開発進捗 ${progress}%`;
  return `Development progress ${progress}%`;
}

export function ReleasePreviewProgressRing({
  progress,
  locale
}: {
  progress: number | null;
  locale: Locale;
}) {
  const normalizedProgress = progress === null || !Number.isFinite(progress)
    ? null
    : Math.min(100, Math.max(0, Math.round(progress)));
  const label = progressLabel(locale, normalizedProgress);

  return (
    <svg
      className={`previewProgressRing${normalizedProgress === null ? " isUnknown" : ""}`}
      viewBox="0 0 16 16"
      fill="none"
      aria-label={label}
      role={normalizedProgress === null ? "img" : "progressbar"}
      {...(normalizedProgress === null ? {} : {
        "aria-valuemin": 0,
        "aria-valuemax": 100,
        "aria-valuenow": normalizedProgress
      })}
    >
      <title>{label}</title>
      <circle className="previewProgressRingTrack" cx="8" cy="8" r="7" />
      {normalizedProgress !== null && (
        <circle
          className="previewProgressRingValue"
          cx="8"
          cy="8"
          r="7"
          pathLength="100"
          transform="rotate(-90 8 8)"
          strokeDasharray="100"
          strokeDashoffset={100 - normalizedProgress}
        />
      )}
    </svg>
  );
}
