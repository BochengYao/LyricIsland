"use client";

import { useEffect, useRef, useState } from "react";
import type { EasterSong } from "@/data/easter-songs";
import styles from "./EasterSongsPage.module.css";

type Layer = { id: number; song: EasterSong; ready: boolean; exiting: boolean };
const fadeCleanupMs = 180;

function BackgroundLayer({ layer, onReady, onExited, onError }: {
  layer: Layer; onReady: (id: number) => void; onExited: (id: number) => void; onError: (id: number) => void;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [ready, setReady] = useState(false);
  const callbacks = useRef({ onReady, onExited, onError });
  callbacks.current = { onReady, onExited, onError };

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const controller = new AbortController();
    let dispose: (() => void) | undefined;
    let firstPaint = 0, secondPaint = 0;
    import("@/lib/song-effect-scene").then(({ createSongEffectScene }) => createSongEffectScene(canvas, layer.song, controller.signal, () => {
      setReady(true);
      // Paint the zero-opacity layer before starting its transition, including
      // when all assets come from the browser cache.
      firstPaint = requestAnimationFrame(() => {
        secondPaint = requestAnimationFrame(() => callbacks.current.onReady(layer.id));
      });
    }, { scrollWithPage: true }))
      .then(cleanup => { if (controller.signal.aborted) cleanup(); else dispose = cleanup; })
      .catch(() => { if (!controller.signal.aborted) callbacks.current.onError(layer.id); });
    return () => { controller.abort(); cancelAnimationFrame(firstPaint); cancelAnimationFrame(secondPaint); dispose?.(); };
  }, [layer.id, layer.song]);

  useEffect(() => {
    if (!layer.exiting) return;
    const delay = matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : fadeCleanupMs;
    const timeout = setTimeout(() => callbacks.current.onExited(layer.id), delay);
    return () => clearTimeout(timeout);
  }, [layer.exiting, layer.id]);

  return <canvas ref={canvasRef} className={styles.songBackground} aria-hidden="true" data-background-effect={layer.song.id} data-effect={layer.song.effect} data-snow={layer.song.assets.includes("snowflake.svg")} data-ready={ready} data-visible={layer.ready && !layer.exiting} data-exiting={layer.exiting} data-paused="false" />;
}

export function SongEffectBackground({ song, onClose, onError }: {
  song: EasterSong | null; onClose: () => void; onError: () => void;
}) {
  const [layers, setLayers] = useState<Layer[]>([]);
  const sequence = useRef(0);
  const latest = useRef(0);
  const callbacks = useRef({ onClose, onError });
  callbacks.current = { onClose, onError };

  useEffect(() => {
    const id = ++sequence.current;
    latest.current = id;
    if (!song) { setLayers(current => current.filter(layer => layer.ready).map(layer => ({ ...layer, exiting: true }))); return; }
    // Keep the previous scene until its replacement is ready. A rapid sequence
    // of clicks retains at most two painted layers and one pending layer.
    setLayers(current => [...current.filter(layer => layer.ready).slice(-2), { id, song, ready: false, exiting: false }]);
  }, [song]);

  const remove = (id: number) => setLayers(current => current.filter(layer => layer.id !== id));
  const reveal = (id: number) => {
    if (id !== latest.current) { remove(id); return; }
    setLayers(current => current.map(layer => layer.id === id ? { ...layer, ready: true } : { ...layer, exiting: true }));
  };
  const fail = (id: number) => { remove(id); if (id === latest.current) callbacks.current.onError(); };

  useEffect(() => {
    if (!song) return;
    const clickBackground = (event: MouseEvent) => {
      const target = event.target;
      if (!(target instanceof Element) || event.defaultPrevented || event.button !== 0) return;
      if (target.closest("a,button,input,select,textarea,label,summary,[role=button],[contenteditable],.siteHeader,footer,#quiz")) return;
      if (!window.getSelection()?.toString()) callbacks.current.onClose();
    };
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape" && !event.defaultPrevented) callbacks.current.onClose(); };
    document.addEventListener("click", clickBackground); document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("click", clickBackground); document.removeEventListener("keydown", escape); };
  }, [song]);

  return <>{layers.map(layer => <BackgroundLayer key={layer.id} layer={layer} onReady={reveal} onExited={remove} onError={fail} />)}</>;
}
