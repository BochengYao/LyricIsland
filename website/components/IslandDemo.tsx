"use client";

import {
  useMemo,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent
} from "react";
import type { SiteCopy } from "@/data/site-copy";

type Props = {
  copy: SiteCopy["demo"];
};

type PlaybackState = "playing" | "idle";

const avoidanceSettings = {
  auraSize: 86,
  detectionRange: 60,
  aspectRatio: 1.27,
  centerTransparency: 0.98,
  transitionTransparency: 0.97
} as const;

export function IslandDemo({ copy }: Props) {
  const [playback, setPlayback] = useState<PlaybackState>("playing");
  const [near, setNear] = useState(false);
  const islandRef = useRef<HTMLDivElement>(null);

  const status = useMemo(() => {
    const values = [
      playback === "playing" ? copy.statusPlaying : copy.statusIdle,
      near ? copy.statusNear : ""
    ];

    return values.filter(Boolean).join(" · ");
  }, [copy, near, playback]);

  const islandClass = [
    "demoIsland",
    playback === "idle" ? "isIdle" : "isPlaying",
    near ? "isNear" : ""
  ]
    .filter(Boolean)
    .join(" ");

  const setPlaybackState = (next: PlaybackState) => {
    setPlayback(next);
    setNear(false);
  };

  const handlePointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const island = islandRef.current;
    if (!island || playback === "idle" || event.pointerType === "touch") {
      setNear(false);
      return;
    }

    const rect = island.getBoundingClientRect();
    const distanceX = Math.max(
      rect.left - event.clientX,
      0,
      event.clientX - rect.right
    );
    const distanceY = Math.max(
      rect.top - event.clientY,
      0,
      event.clientY - rect.bottom
    );
    const distance = Math.hypot(distanceX, distanceY);
    const normalizedDistance = Math.min(
      1,
      distance / avoidanceSettings.detectionRange
    );
    const avoidanceStrength = 1 - normalizedDistance * normalizedDistance;
    const isNearby = avoidanceStrength > 0.001;

    setNear((current) => (current === isNearby ? current : isNearby));
    if (!isNearby) {
      island.style.setProperty("--avoid-radius-x", "0px");
      island.style.setProperty("--avoid-radius-y", "0px");
      island.style.setProperty("--avoid-center-opacity", "1");
      island.style.setProperty("--avoid-transition-opacity", "1");
      return;
    }

    const avoidX = Math.min(
      rect.width,
      Math.max(0, event.clientX - rect.left)
    );
    const avoidY = Math.min(
      rect.height,
      Math.max(0, event.clientY - rect.top)
    );
    island.style.setProperty("--avoid-x", `${avoidX}px`);
    island.style.setProperty("--avoid-y", `${avoidY}px`);
    const shapeScale = Math.sqrt(avoidanceSettings.aspectRatio);
    island.style.setProperty(
      "--avoid-radius-x",
      `${avoidanceSettings.auraSize * shapeScale * avoidanceStrength}px`
    );
    island.style.setProperty(
      "--avoid-radius-y",
      `${(avoidanceSettings.auraSize / shapeScale) * avoidanceStrength}px`
    );
    island.style.setProperty(
      "--avoid-center-opacity",
      `${1 - avoidanceSettings.centerTransparency * avoidanceStrength}`
    );
    island.style.setProperty(
      "--avoid-transition-opacity",
      `${1 - avoidanceSettings.transitionTransparency * avoidanceStrength}`
    );
  };

  return (
    <div className="demoShell">
      <div className="demoControls">
        <div
          className="demoControlGroup"
          role="group"
          aria-label={copy.playbackLabel}
        >
          <span className="demoControlLabel">{copy.playbackLabel}</span>
          <div className="segmentedControl">
            <button
              type="button"
              aria-pressed={playback === "playing"}
              onClick={() => setPlaybackState("playing")}
            >
              {copy.playing}
            </button>
            <button
              type="button"
              aria-pressed={playback === "idle"}
              onClick={() => setPlaybackState("idle")}
            >
              {copy.idle}
            </button>
          </div>
        </div>
      </div>

      <div
        className="demoDesktop"
        aria-label={copy.title}
        onPointerMove={handlePointerMove}
        onPointerLeave={() => setNear(false)}
      >
        <span className="demoDesktopLabel">Windows desktop</span>
        <div
          ref={islandRef}
          className={islandClass}
          data-testid="demo-island"
        >
          <div className="demoIslandSurface">
            <svg
              className="demoIslandShape"
              viewBox="0 0 560 60"
              preserveAspectRatio="none"
              aria-hidden="true"
            >
              <path d="M 0,0 L 560,0 C 532,0 522,5 516,15 C 512,22 512,34 512,40 C 512,49 504,55 491,55 L 69,55 C 56,55 48,49 48,40 C 48,34 48,22 44,15 C 38,5 28,0 0,0 Z" />
            </svg>
            <div className="demoIslandContent">
              <div className="albumModule" aria-hidden="true">
                <span>MS</span>
              </div>
              <div className="trackModule">
                <span className="islandEyebrow">{copy.nowPlaying}</span>
                <strong>{copy.track}</strong>
                <small>{copy.artist}</small>
              </div>
              <span className="islandDivider" aria-hidden="true" />
              <div className="lyricModule">
                <strong>{copy.lyric}</strong>
                <small>{copy.translation}</small>
              </div>
              <div className="progressModule" aria-hidden="true">
                <span className="progressTrack">
                  <span />
                </span>
                <small>0:53 / 3:43</small>
              </div>
              <div className="controlModule" aria-hidden="true">
                <span>◀</span>
                <span className="pauseControl">Ⅱ</span>
                <span>▶</span>
              </div>
            </div>
          </div>
        </div>
        <div className="demoWindow" aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
      </div>

      <p className="srOnly" aria-live="polite">
        {status}
      </p>
    </div>
  );
}
