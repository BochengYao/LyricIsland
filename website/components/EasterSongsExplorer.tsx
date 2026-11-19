"use client";
import { useState } from "react";
import { easterCopy, easterSongs, songEffectName, type EasterSong } from "@/data/easter-songs";
import { playCopy } from "@/data/easter-play-copy";
import { drawSong, newQuestion } from "@/lib/easter-song-games";
import type { Locale } from "@/data/site-copy";
import { SongEffectClue } from "./SongEffectClue";
import { SongEffectBackground } from "./SongEffectBackground";
import { DecorativeStar } from "./DecorativeStar";
import styles from "./EasterSongsPage.module.css";

export function EasterSongsExplorer({ locale }: { locale: Locale }) {
  const copy = easterCopy[locale], play = playCopy[locale];
  const [drawn, setDrawn] = useState<EasterSong | null>(null);
  const [query, setQuery] = useState("");
  const [question, setQuestion] = useState<ReturnType<typeof newQuestion> | null>(null);
  const [choice, setChoice] = useState<string | null>(null);
  const [score, setScore] = useState({ correct: 0, answered: 0 });
  const [previewSong, setPreviewSong] = useState<EasterSong | null>(null);
  const [previewError, setPreviewError] = useState(false);

  function preview(song: EasterSong) {
    setPreviewError(false);
    setPreviewSong(previous => previous?.id === song.id ? null : song);
  }

  function answer(id: string) {
    if (!question || choice !== null) return;
    setChoice(id);
    setScore(previous => ({ correct: previous.correct + (id === question.answer.id ? 1 : 0), answered: previous.answered + 1 }));
  }
  function nextQuestion() { setQuestion(newQuestion(question?.answer.id)); setChoice(null); }
  const normalized = query.trim().toLocaleLowerCase();
  const filtered = easterSongs.filter(song => [song.title, ...song.aliases, songEffectName(song, locale)].some(text => text.toLocaleLowerCase().includes(normalized)));

  return <>
    <SongEffectBackground song={previewSong} onClose={() => setPreviewSong(null)} onError={() => { setPreviewSong(null); setPreviewError(true); }} />
    <nav className={styles.sectionLinks} aria-label={copy.nav}><a href="#daily">01 / {play.draw}</a><a href="#playlist">02 / {copy.list}</a><a href="#quiz">03 / {play.quiz}</a></nav>
    <section className={styles.daily} id="daily" aria-labelledby="daily-title">
      <div className={styles.dailyIntro}><span className={styles.kicker}>01 / A LITTLE CHANCE</span><h2 id="daily-title">{play.draw}</h2><p>{play.drawNote}</p></div>
      <div className={styles.drawArea}>
        {!drawn ? <button className={styles.sleeve} type="button" onClick={() => setDrawn(drawSong())} aria-label={play.sleeve}><span className={styles.sleeveTop}>LYRIC HOVER / HIDDEN TRACKS</span><DecorativeStar className={styles.sleeveArt} /><strong>{play.drawHint}</strong><span className={styles.sleeveBottom}>SIDE A <span>↗</span> YOUR NEXT FAVORITE</span></button>
          : <div className={styles.drawResult} key={drawn.id}><span className={styles.kicker}>{play.result}</span><h3>{drawn.title}</h3><p className={styles.artist}>{drawn.artist}</p><div className={styles.fact}><small>{play.effect}</small><strong>{songEffectName(drawn, locale)}</strong></div><div className={styles.resultActions}><button type="button" onClick={() => setDrawn(drawSong(drawn.id))}>{play.drawAgain} ↻</button></div></div>}
      </div>
    </section>

    <section className={styles.playlist} id="playlist" aria-labelledby="playlist-title">
      <div className={styles.playlistHeading}><div><span className={styles.kicker}>02 / THE PLAYLIST</span><div className={styles.playlistTitle}><h2 id="playlist-title">{copy.list}</h2><p className={styles.playlistHint}>{play.previewHint}</p></div></div><label className={styles.search}><span aria-hidden="true">⌕</span><input type="search" aria-label={copy.search} placeholder={copy.search} value={query} onChange={event => setQuery(event.target.value)} /></label></div>
      <p className={styles.results} role="status">{filtered.length} / {easterSongs.length} {copy.count}</p>
      {previewError && <p className={styles.backgroundError} role="alert">{play.previewError}</p>}
      <ul className={styles.songs}>{filtered.map(song => <li key={song.id} data-song-id={song.id}><button type="button" className={styles.songRow} aria-pressed={previewSong?.id === song.id} data-preview={previewSong?.id === song.id ? "playing" : undefined} title={play.backgroundHint} aria-label={`${previewSong?.id === song.id ? play.stopPreview : play.previewSong}: ${song.title} — ${song.artist}`} onClick={() => preview(song)}>
        <span className={styles.songNumber} aria-hidden="true">{String(easterSongs.indexOf(song) + 1).padStart(2, "0")}</span>
        <span className={styles.songInfo}><strong>{song.title}</strong><small>{song.artist}</small></span>
        <span className={styles.effectTag}>{songEffectName(song, locale)}</span>
      </button></li>)}</ul>
      {!filtered.length && <div className={styles.empty}><h3>{copy.empty}</h3><p>{copy.emptyNote}</p><button type="button" className={styles.textButton} onClick={() => setQuery("")}>{copy.reset}</button></div>}
    </section>

    <section className={styles.quiz} id="quiz" aria-labelledby="quiz-title">
      <div className={styles.quizIntro}><span className={styles.kicker}>03 / NAME THAT SONG</span><h2 id="quiz-title">{play.quiz}</h2><p>{play.quizNote}</p><p className={styles.score} role="status">{play.score} <strong>{score.correct}</strong> / {score.answered}</p></div>
      <div className={styles.quizGame}>{!question ? <div className={styles.quizStart}><span aria-hidden="true">?</span><button type="button" className={styles.darkButton} onClick={nextQuestion}>{play.start}</button></div> : <>
        <span className={styles.kicker}>{play.round} {String(score.answered + (choice === null ? 1 : 0)).padStart(2, "0")}</span>
        <p className={styles.clue}>{play.question}</p>
        <SongEffectClue key={question.answer.id} song={question.answer} locale={locale} answered={choice !== null} />
        <div className={styles.quizOptions} role="group" aria-label={play.options}>{question.options.map((song, index) => <button type="button" key={song.id} onClick={() => answer(song.id)} disabled={choice !== null} data-answer={choice === null ? undefined : song.id === question.answer.id ? "correct" : song.id === choice ? "wrong" : "other"}><span aria-hidden="true">{String.fromCharCode(65 + index)}</span><span><strong>{song.title}</strong><small>{song.artist}</small></span><span aria-hidden="true">{choice !== null && song.id === question.answer.id ? "✓" : ""}</span></button>)}</div>
        {choice !== null && <div className={styles.quizFeedback} role="status"><p>{choice === question.answer.id ? play.correct : play.wrong} <strong>{question.answer.title}</strong></p><p>{question.answer.artist}</p><button type="button" className={styles.darkButton} onClick={nextQuestion}>{play.next}</button></div>}
      </>}</div>
    </section>
  </>;
}
