import type { Locale } from "@/data/site-copy";
import type { ReleasePreviewFeatureStage } from "@/data/incentives-types";

type PreviewRingState = "notStarted" | "inProgress" | "testing" | "ready";

function normalizeProgress(progress: number) {
  return Number.isFinite(progress) ? Math.min(100, Math.max(0, Math.round(progress))) : 0;
}

function stateFor(progress: number, stage: ReleasePreviewFeatureStage): PreviewRingState {
  if (stage === "ready" && progress === 100) return "ready";
  if (stage === "testing" && progress === 100) return "testing";
  if (progress === 0) return "notStarted";
  if (progress === 100) return "testing";
  return "inProgress";
}

function stateLabel(locale: Locale, state: PreviewRingState, progress: number) {
  if (locale === "zh") {
    if (state === "notStarted") return "未开始";
    if (state === "testing") return "测试中";
    if (state === "ready") return "待上线";
    return `开发中，进度 ${progress}%`;
  }
  if (locale === "zhHant") {
    if (state === "notStarted") return "尚未開始";
    if (state === "testing") return "測試中";
    if (state === "ready") return "等待上線";
    return `開發中，進度 ${progress}%`;
  }
  if (locale === "ja") {
    if (state === "notStarted") return "未着手";
    if (state === "testing") return "テスト中";
    if (state === "ready") return "リリース待ち";
    return `開発中、進捗 ${progress}%`;
  }
  if (state === "notStarted") return "Not started";
  if (state === "testing") return "Testing";
  if (state === "ready") return "Ready for release";
  return `In development, ${progress}% complete`;
}

export function ReleasePreviewProgressRing({
  progress,
  stage = "development",
  locale,
  decorative = false
}: {
  progress: number;
  stage?: ReleasePreviewFeatureStage;
  locale: Locale;
  decorative?: boolean;
}) {
  const normalizedProgress = normalizeProgress(progress);
  const state = stateFor(normalizedProgress, stage);
  const label = stateLabel(locale, state, normalizedProgress);
  const isProgress = state === "inProgress";

  return (
    <svg
      className={`previewProgressRing is${state[0].toUpperCase()}${state.slice(1)}`}
      viewBox="0 0 16 16"
      fill="none"
      aria-hidden={decorative || undefined}
      aria-label={decorative ? undefined : label}
      role={decorative ? undefined : isProgress ? "progressbar" : "img"}
      {...(!decorative && isProgress ? {
        "aria-valuemin": 0,
        "aria-valuemax": 100,
        "aria-valuenow": normalizedProgress,
        "aria-valuetext": label
      } : {})}
    >
      {!decorative && <title>{label}</title>}
      {state !== "testing" && state !== "ready" && (
        <circle className="previewProgressRingTrack" cx="8" cy="8" r="7" />
      )}
      {state !== "notStarted" && (
        <circle
          className="previewProgressRingValue"
          cx="8"
          cy="8"
          r="7"
          pathLength="100"
          transform="rotate(-90 8 8)"
          strokeDasharray="100"
          strokeDashoffset={state === "inProgress" ? 100 - normalizedProgress : 0}
        />
      )}
    </svg>
  );
}
