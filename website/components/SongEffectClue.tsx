"use client";
import { useEffect, useRef, useState } from "react";
import { songEffectName, type EasterSong } from "@/data/easter-songs";
import { playCopy } from "@/data/easter-play-copy";
import type { Locale } from "@/data/site-copy";
import styles from "./EasterSongsPage.module.css";

export function SongEffectClue({ song, locale, answered }: { song: EasterSong; locale: Locale; answered: boolean }) {
  const ref = useRef<HTMLCanvasElement>(null);
  const [ready, setReady] = useState(false);
  const [failed, setFailed] = useState(false);
  const [finished, setFinished] = useState(false);
  const [replay, setReplay] = useState(0);
  const copy = playCopy[locale];
  useEffect(() => {
    const canvas = ref.current;
    if (!canvas) return;
    const controller = new AbortController();
    let dispose: (() => void) | undefined;
    setReady(false); setFailed(false); setFinished(false);
    import("@/lib/song-effect-scene").then(({ createSongEffectScene }) => createSongEffectScene(canvas, song, controller.signal, () => setReady(true)))
      .then(cleanup => { if (controller.signal.aborted) cleanup(); else { dispose = cleanup; canvas.dispatchEvent(new Event("effectpause")); } })
      .catch(() => { if (!controller.signal.aborted) setFailed(true); });
    return () => { controller.abort(); dispose?.(); };
  }, [song, replay]);
  useEffect(() => {
    if (!ready) return;
    const timeout = setTimeout(() => setFinished(true), 5000);
    return () => clearTimeout(timeout);
  }, [ready, replay]);
  useEffect(() => { if (answered) setFinished(true); }, [answered]);
  useEffect(() => { ref.current?.dispatchEvent(new Event("effectpause")); }, [finished]);
  return <figure className={styles.visualClue}>
    <div className={styles.clueScene}>
      <canvas ref={ref} role="img" aria-label={`${copy.visualLabel}: ${songEffectName(song, locale)}`} data-effect={song.effect} data-ready={ready} data-paused={finished} />
      {!ready && !failed && <span className={styles.sceneMessage} role="status">{copy.loading}</span>}
      {failed && <span className={styles.sceneMessage} role="status">{copy.effectFailed}</span>}
    </div>
    <figcaption><span>{!answered && copy.watchClue}</span><button type="button" onClick={() => setReplay(previous => previous + 1)} disabled={!ready && !failed}>{failed ? copy.retry : copy.replay} ↻</button></figcaption>
    {failed && <p className={styles.clueFallback}>{songEffectName(song, locale)}</p>}
  </figure>;
}
