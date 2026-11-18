import catalog from "./song-effects-catalog.json";
import facts from "./song-effect-facts.json";
import type { Locale } from "./site-copy";

const localeIndex: Record<Locale, number> = { zh: 0, zhHant: 1, en: 2, ja: 3 };
export const effectSignature = (effect: { type: string; assets: string[] }) => `${effect.type}:${[...effect.assets].sort().join(",")}`;
export const easterSongs = catalog.entries.map((entry) => {
  const signature = effectSignature(entry.effect);
  const names = (facts as Record<string, string[]>)[signature];
  if (!names) throw new Error(`Unverified effect: ${signature}`);
  return { id: entry.id, title: entry.match.titleBase, artist: entry.match.artistAliases[0], aliases: entry.match.artistAliases,
    effect: entry.effect.type, definition: entry.effect, assets: entry.effect.assets, signature, names };
});
export type EasterSong = (typeof easterSongs)[number];
export const songEffectName = (song: EasterSong, locale: Locale) => song.names[localeIndex[locale]];

export const easterCopy = {
  zh: {
    nav: "彩蛋歌曲", eyebrow: "歌曲彩蛋", title: "听歌，\n也有看头。", intro: "打开全屏歌词，播放《All Too Well》，看枫叶缓缓落下。换一首《Firework》，烟花随之绽放。",
    list: "彩蛋歌单", search: "搜索歌曲、歌手或彩蛋",
    count: "首歌曲", empty: "还没找到这首歌", emptyNote: "试试歌手名，或换个关键词。", reset: "清空筛选",
    how: "把惊喜带回桌面", steps: ["播放歌单中的歌曲。", "打开全屏歌词。", "在设置中开启「歌曲彩蛋」。"], download: "去下载歌词岛", home: "回到主页", footer: "让喜欢的歌，再多一点喜欢。", skip: "跳到彩蛋歌单",
  },
  zhHant: {
    nav: "彩蛋歌曲", eyebrow: "歌曲彩蛋", title: "聽歌，\n也有看頭。", intro: "開啟全螢幕歌詞，播放《All Too Well》，看楓葉緩緩落下。換一首《Firework》，煙花隨之綻放。",
    list: "彩蛋歌單", search: "搜尋歌曲、歌手或彩蛋",
    count: "首歌曲", empty: "還沒找到這首歌", emptyNote: "試試歌手名，或換個關鍵字。", reset: "清空篩選",
    how: "把驚喜帶回桌面", steps: ["播放歌單中的歌曲。", "開啟全螢幕歌詞。", "在設定中開啟「歌曲彩蛋」。"], download: "下載歌詞島", home: "回到首頁", footer: "讓喜歡的歌，再多一點喜歡。", skip: "跳至彩蛋歌單",
  },
  en: {
    nav: "Easter egg songs", eyebrow: "SONG EASTER EGGS", title: "Music that’s\nworth a look.", intro: "Open fullscreen lyrics and play All Too Well to watch maple leaves fall. Switch to Firework, and fireworks begin to bloom.",
    list: "The secret playlist", search: "Search songs, artists or effects",
    count: "songs", empty: "No songs found", emptyNote: "Try an artist name or another keyword.", reset: "Clear filters",
    how: "Take the surprise to your desktop", steps: ["Play a song from the playlist.", "Open fullscreen lyrics.", "Enable “Song Easter eggs” in settings."], download: "Get LyricHover", home: "Back to home", footer: "One more reason to love your favorite songs.", skip: "Skip to the playlist",
  },
  ja: {
    nav: "隠し演出の曲", eyebrow: "曲の隠し演出", title: "聴くだけでなく、\n見る楽しみも。", intro: "全画面歌詞を開き、All Too Well を再生すると、楓の葉がゆっくり舞い落ちます。Firework に切り替えると、花火が広がります。",
    list: "秘密のプレイリスト", search: "曲名・アーティスト・演出で検索",
    count: "曲", empty: "曲が見つかりません", emptyNote: "アーティスト名や別のキーワードをお試しください。", reset: "絞り込みを解除",
    how: "サプライズをデスクトップに", steps: ["リストから一曲再生。", "全画面歌詞を開く。", "設定で「曲のイースターエッグ」をオンに。"], download: "LyricHover をダウンロード", home: "ホームに戻る", footer: "好きな曲が、もう少し好きになる。", skip: "プレイリストへ移動",
  },
} satisfies Record<Locale, object>;
