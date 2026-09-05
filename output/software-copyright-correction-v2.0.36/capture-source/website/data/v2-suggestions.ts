import type { PublicSuggestion } from "@/data/incentives-types";
import type { Locale } from "@/data/site-copy";

const base = [
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a001",
    nickname: "歌词岛 v2",
    created_at: "2026-06-18T09:20:00+08:00",
    zh: ["模块自由布局", "封面、歌词、播放控制、歌曲信息、进度与分割线，都能按习惯重新排列。"],
    en: ["Modular layouts", "Arrange artwork, lyrics, controls, track info, progress, and dividers around the way you listen."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a002",
    nickname: "歌词岛 v2",
    created_at: "2026-06-22T14:05:00+08:00",
    zh: ["直接拖动真实歌词岛", "从模块工具箱拖到真实歌词岛，支持 18 px 吸附、重排、保存与取消。"],
    en: ["Edit the real island", "Drag modules onto the real island with 18 px snapping, reordering, save, and cancel flows."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a003",
    nickname: "歌词岛 v2",
    created_at: "2026-06-27T20:40:00+08:00",
    zh: ["A / C 双布局", "A 与 C 模式各自保存布局；C 模式保持紧凑，鼠标悬停后再展开。"],
    en: ["Independent A / C layouts", "A and C modes keep separate layouts, with compact C mode expanding on hover."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a004",
    nickname: "歌词岛 v2",
    created_at: "2026-07-01T11:18:00+08:00",
    zh: ["锁定常用播放器", "自动跟随最近活跃播放器，也可以在设置里固定某个指定播放器。"],
    en: ["Lock a preferred player", "Follow the most recently active player or lock a specific compatible player in settings."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a005",
    nickname: "歌词岛 v2",
    created_at: "2026-07-03T16:32:00+08:00",
    zh: ["歌词源自动补位", "可选择首选歌词源；匹配不理想时，歌词岛会临时尝试其他已支持来源。"],
    en: ["Lyric-source fallback", "Choose a preferred provider while the app temporarily tries other supported sources when needed."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a006",
    nickname: "歌词岛 v2",
    created_at: "2026-07-05T19:46:00+08:00",
    zh: ["长歌词不再省略", "长句会根据歌词时长横向滚动，让一句歌词完整经过，而不是被省略号切断。"],
    en: ["Full long-line lyrics", "Long lines scroll across their lyric duration instead of being cut off with an ellipsis."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a007",
    nickname: "歌词岛 v2",
    created_at: "2026-07-08T10:12:00+08:00",
    zh: ["局部鼠标避让", "只有鼠标附近的歌词岛区域降低透明度，探测范围、光晕与频谱都可以调整。"],
    en: ["Local cursor avoidance", "Only the area around the cursor fades, with configurable detection, aura, and opacity spectrum."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a008",
    nickname: "歌词岛 v2",
    created_at: "2026-07-10T13:54:00+08:00",
    zh: ["浅色、深色、跟随系统", "新版偏好设置可以选择界面主题，并集中调整布局、播放器、缓存与避让效果。"],
    en: ["Light, dark, or system", "The refreshed preferences bring theme, layout, player, cache, and avoidance controls together."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a009",
    nickname: "歌词岛 v2",
    created_at: "2026-07-12T08:25:00+08:00",
    zh: ["同步歌词与歌词库翻译", "优先使用歌词源已经提供的逐行同步歌词和中文翻译，不额外生成机器翻译。"],
    en: ["Synced lyrics and translations", "Prefer synced lines and translations already supplied by lyric providers, without generated machine translation."]
  },
  {
    id: "21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a00a",
    nickname: "歌词岛 v2",
    created_at: "2026-07-13T21:06:00+08:00",
    zh: ["本地 LRU 歌词缓存", "按歌曲管理本地歌词缓存，旧内容会按容量自动淘汰，也能在设置里调整缓存大小。"],
    en: ["Local LRU lyric cache", "A song-level local cache evicts older entries by capacity, with size controls in preferences."]
  }
] as const;

export function v2Suggestions(locale: Locale): PublicSuggestion[] {
  return base.map((item) => ({
    id: item.id,
    nickname: item.nickname,
    created_at: item.created_at,
    title: item[locale][0],
    body: item[locale][1],
    kind: "feature",
    developer_reply: null,
    like_count: 0,
    liked: false
  }));
}
