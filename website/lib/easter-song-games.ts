import { easterSongs, type EasterSong } from "@/data/easter-songs";

export function drawSong(previous?: string, random = Math.random): EasterSong {
  const pool = easterSongs.filter(song => song.id !== previous);
  return pool[Math.floor(random() * pool.length)];
}
export function newQuestion(previous?: string, random = Math.random) {
  const answer = drawSong(previous, random);
  // Last Christmas and All I Want for Christmas Is You share the same snow effect.
  // Never offer two valid answers to the same clue.
  const distractors = easterSongs.filter(song => song.signature !== answer.signature);
  const shuffled = <T,>(items: T[]) => {
    const result = [...items];
    for (let i = result.length - 1; i > 0; i--) { const j = Math.floor(random() * (i + 1)); [result[i], result[j]] = [result[j], result[i]]; }
    return result;
  };
  return { answer, options: shuffled([answer, ...shuffled(distractors).slice(0, 2)]) };
}
