import type { Locale } from "@/data/site-copy";
import type { ReleasePreviewFeatureStage } from "@/data/incentives-types";

type PreviewRingState = "notStarted" | "inProgress" | "testing" | "ready";

type LegendItem = {
  state: PreviewRingState;
  progress: number;
  stage: ReleasePreviewFeatureStage;
};

const legendItems: LegendItem[] = [
  { state: "notStarted", progress: 0, stage: "development" },
  { state: "inProgress", progress: 65, stage: "development" },
  { state: "testing", progress: 100, stage: "development" },
  { state: "ready", progress: 100, stage: "ready" }
];

function normalizeProgress(progress: number) {
  return Number.isFinite(progress) ? Math.min(100, Math.max(0, Math.round(progress))) : 0;
}

function stateFor(progress: number, stage: ReleasePreviewFeatureStage): PreviewRingState {
  if (stage === "ready" && progress === 100) return "ready";
  if (progress === 0) return "notStarted";
  if (progress === 100) return "testing";
  return "inProgress";
}

function stateLabel(locale: Locale, state: PreviewRingState, progress: number) {
  if (locale === "zh") {
    if (state === "notStarted") return "未开始";
    if (state === "testing") return "开发完成，待测试";
    if (state === "ready") return "测试完成，待上线";
    return `开发中，进度 ${progress}%`;
  }
  if (locale === "zhHant") {
    if (state === "notStarted") return "尚未開始";
    if (state === "testing") return "開發完成，等待測試";
    if (state === "ready") return "測試完成，等待上線";
    return `開發中，進度 ${progress}%`;
  }
  if (locale === "ja") {
    if (state === "notStarted") return "未着手";
    if (state === "testing") return "開発完了・テスト待ち";
    if (state === "ready") return "テスト完了・リリース待ち";
    return `開発中、進捗 ${progress}%`;
  }
  if (state === "notStarted") return "Not started";
  if (state === "testing") return "Development complete, awaiting testing";
  if (state === "ready") return "Testing complete, ready for release";
  return `In development, ${progress}% complete`;
}

function legendLabel(locale: Locale) {
  if (locale === "zh") return "开发状态图例";
  if (locale === "zhHant") return "開發狀態圖例";
  if (locale === "ja") return "開発状態の凡例";
  return "Development status legend";
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
      <circle className="previewProgressRingTrack" cx="8" cy="8" r="7" />
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

export function ReleasePreviewLegend({ locale }: { locale: Locale }) {
  return (
    <div className="previewLegend" aria-label={legendLabel(locale)}>
      <span className="previewLegendTitle">{legendLabel(locale)}</span>
      <ul>
        {legendItems.map((item) => (
          <li key={item.state}>
            <ReleasePreviewProgressRing {...item} locale={locale} decorative />
            <span>{stateLabel(locale, item.state, item.progress)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
