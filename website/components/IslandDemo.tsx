"use client";

import { useMemo, useState } from "react";
import type { SiteCopy } from "@/data/site-copy";

type Props = {
  copy: SiteCopy["demo"];
};

type PlaybackState = "playing" | "idle";
type LayoutMode = "a" | "c";

export function IslandDemo({ copy }: Props) {
  const [playback, setPlayback] = useState<PlaybackState>("playing");
  const [near, setNear] = useState(false);
  const [layout, setLayout] = useState<LayoutMode>("a");

  const status = useMemo(() => {
    const values = [
      playback === "playing" ? copy.statusPlaying : copy.statusIdle,
      near ? copy.statusNear : "",
      layout === "a" ? copy.statusA : copy.statusC
    ];

    return values.filter(Boolean).join(" · ");
  }, [copy, layout, near, playback]);

  const islandClass = [
    "demoIsland",
    playback === "idle" ? "isIdle" : "isPlaying",
    near ? "isNear" : "",
    layout === "c" ? "isLayoutC" : "isLayoutA"
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className="demoShell">
      <div className="demoControls">
        <fieldset>
          <legend>{copy.playbackLabel}</legend>
          <div className="segmentedControl">
            <button
              type="button"
              aria-pressed={playback === "playing"}
              onClick={() => setPlayback("playing")}
            >
              {copy.playing}
            </button>
            <button
              type="button"
              aria-pressed={playback === "idle"}
              onClick={() => setPlayback("idle")}
            >
              {copy.idle}
            </button>
            <button
              type="button"
              aria-pressed={near}
              onClick={() => setNear((value) => !value)}
            >
              {copy.near}
            </button>
          </div>
        </fieldset>
        <fieldset>
          <legend>{copy.layoutLabel}</legend>
          <div className="segmentedControl">
            <button
              type="button"
              aria-pressed={layout === "a"}
              onClick={() => setLayout("a")}
            >
              {copy.layoutA}
            </button>
            <button
              type="button"
              aria-pressed={layout === "c"}
              onClick={() => setLayout("c")}
            >
              {copy.layoutC}
            </button>
          </div>
        </fieldset>
      </div>

      <div className="demoDesktop" aria-label={copy.title}>
        <span className="demoDesktopLabel">Windows desktop</span>
        <div className={islandClass} data-testid="demo-island">
          <div className="albumModule" aria-hidden="true">
            <span>LI</span>
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
          <div className="controlModule" aria-hidden="true">
            <span>‹</span>
            <span className="playDisc">Ⅱ</span>
            <span>›</span>
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
