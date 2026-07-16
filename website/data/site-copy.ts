export type Locale = "zh" | "en";

export type SiteCopy = {
  languageName: string;
  languageHref: string;
  navLabel: string;
  menuLabel: string;
  nav: Array<{
    label: string;
    href: string;
    external?: boolean;
    kind?: "feature" | "store";
  }>;
  eyebrow: string;
  heroTitle: string;
  heroBody: string;
  storeLabel: string;
  exploreLabel: string;
  heroImageAlt: string;
  heroBadge: string;
  heroIslandTitle: string;
  heroIslandSub: string;
  experience: {
    eyebrow: string;
    title: string;
    body: string;
    watermark: string;
    items: Array<{
      tag: string;
      title: string;
      body: string;
      image: string;
      imageAlt: string;
      imagePosition: string;
    }>;
  };
  demo: {
    eyebrow: string;
    title: string;
    body: string;
    playbackLabel: string;
    layoutLabel: string;
    playing: string;
    idle: string;
    near: string;
    layoutA: string;
    layoutC: string;
    nowPlaying: string;
    track: string;
    artist: string;
    lyric: string;
    translation: string;
    statusPlaying: string;
    statusIdle: string;
    statusNear: string;
    statusA: string;
    statusC: string;
  };
  modules: {
    eyebrow: string;
    title: string;
    body: string;
    editorNote: string;
    names: string[];
  };
  compatibility: {
    eyebrow: string;
    title: string;
    body: string;
    note: string;
    players: string[];
  };
  sources: {
    eyebrow: string;
    title: string;
    body: string;
    facts: Array<{ value: string; label: string; detail: string }>;
  };
  faq: {
    eyebrow: string;
    title: string;
    items: Array<{ question: string; answer: string }>;
  };
  closing: {
    eyebrow: string;
    title: string;
    body: string;
    button: string;
    communityButton: string;
  };
  footer: {
    title: string;
    product: string;
    productLinks: Array<{ label: string; href: string }>;
    resources: string;
    resourceLinks: Array<{ label: string; href: string }>;
    note: string;
    copyright: string;
  };
};

const sharedPlayers = [
  "Apple Music",
  "QQ Music",
  "NetEase Cloud Music",
  "KuGou",
  "Spotify",
  "KuWo",
  "SMTC"
];

export const microsoftStoreUrl =
  "https://apps.microsoft.com/detail/9nrxzp5hmxk2?hl=zh-CN&gl=CN";

export const copyByLocale: Record<Locale, SiteCopy> = {
  zh: {
    languageName: "EN",
    languageHref: "/en",
    navLabel: "主导航",
    menuLabel: "打开导航",
    nav: [
      { label: "主页", href: "#main" },
      { label: "新功能", href: "/updates", kind: "feature" },
      { label: "用户激励计划", href: "/incentives" },
      {
        label: "Microsoft Store",
        href: microsoftStoreUrl,
        external: true,
        kind: "store"
      }
    ],
    eyebrow: "Windows 桌面歌词伴侣",
    heroTitle: "歌词，停在工作上方。",
    heroBody:
      "播放时从屏幕顶端轻轻滑入，空闲时完整收起。歌词岛把同步歌词留在视线边缘，让音乐陪伴工作，却不打断工作。",
    storeLabel: "Microsoft Store 稳定版",
    exploreLabel: "看看它如何工作",
    heroImageAlt: "歌词岛显示在 Windows 桌面顶部",
    heroBadge: "v2.0 Beta 1",
    heroIslandTitle: "City lights above the screen",
    heroIslandSub: "城市光停在屏幕边缘",
    experience: {
      eyebrow: "出现得刚刚好",
      title: "像一座岛，知道什么时候靠岸。",
      body:
        "歌词岛不占据任务栏，也不制造新的窗口负担。它跟随播放状态行动，并在鼠标靠近时主动让出屏幕内容。",
      watermark: "QUIETLY ABOVE",
      items: [
        {
          tag: "播放",
          title: "开始播放，歌词自然滑入",
          body: "读取 Windows 当前媒体会话，让歌词从顶部边缘进入视线。",
          image: "/images/product-hero.png",
          imageAlt: "歌词岛在播放时显示于屏幕顶部",
          imagePosition: "72% 8%"
        },
        {
          tag: "空闲",
          title: "暂停之后，完整收回屏幕外",
          body: "没有正在播放的内容时，桌面恢复安静，不留下悬浮占位。",
          image: "/images/product-focus.png",
          imageAlt: "工作界面中的顶部歌词岛",
          imagePosition: "50% 5%"
        },
        {
          tag: "避让",
          title: "鼠标靠近，只让附近变轻",
          body: "按光晕范围降低局部背景和文字不透明度，下面的内容仍然可读、可点。",
          image: "/images/product-modules.png",
          imageAlt: "歌词岛与 Windows 桌面共存",
          imagePosition: "12% 52%"
        }
      ]
    },
    demo: {
      eyebrow: "亲手感受",
      title: "让歌词岛跟着你的状态变化。",
      body:
        "切换播放、空闲、鼠标靠近和布局模式。这里的示例完全在浏览器中运行，不会连接你的播放器。",
      playbackLabel: "播放状态",
      layoutLabel: "布局模式",
      playing: "播放",
      idle: "空闲",
      near: "鼠标靠近",
      layoutA: "A 横向积木",
      layoutC: "C 双态展开",
      nowPlaying: "正在播放",
      track: "Quiet Orbit",
      artist: "Lyric Island",
      lyric: "城市灯光停在屏幕边缘",
      translation: "City lights rest above the screen",
      statusPlaying: "歌词岛已滑入",
      statusIdle: "歌词岛已收起",
      statusNear: "鼠标避让已开启",
      statusA: "当前为 A 横向积木布局",
      statusC: "当前为 C 双态展开布局"
    },
    modules: {
      eyebrow: "由你组合",
      title: "不只是歌词，而是你的音乐抬头显示。",
      body:
        "v2.0 Beta 1 把歌词岛拆成可组合模块。A 与 C 布局独立保存，C 模式在悬停后展开更多信息。",
      editorNote: "在设置中把模块拖到真实歌词岛，18 px 吸附、重排、保存或取消。",
      names: ["专辑封面", "同步歌词", "播放控制", "歌曲信息", "播放进度", "分割线"]
    },
    compatibility: {
      eyebrow: "跟随正在播放的声音",
      title: "一个歌词岛，适配多个 Windows 播放器。",
      body:
        "通过 Windows SMTC 读取当前媒体会话。你可以跟随最近活跃的播放器，也可以在设置里锁定指定播放器。",
      note: "具体控制和时间轴能力取决于播放器通过 Windows 提供的信息。",
      players: ["Apple Music", "QQ 音乐", "网易云音乐", "酷狗音乐", "Spotify", "酷我音乐", "通用 SMTC"]
    },
    sources: {
      eyebrow: "歌词来自哪里",
      title: "多来源匹配，歌词与翻译保持原样。",
      body:
        "支持 LRCLIB、QQ 音乐、酷狗和网易云等来源。优先使用歌词库已有的同步歌词和中文翻译，不自行生成机器翻译。",
      facts: [
        { value: "4+", label: "歌词来源", detail: "首选源不匹配时自动尝试其他来源" },
        { value: "LRU", label: "本地缓存", detail: "按歌曲维度维护缓存容量" },
        { value: "1", label: "运行实例", detail: "重复启动会回到现有实例" }
      ]
    },
    faq: {
      eyebrow: "常见问题",
      title: "开始之前，你可能想知道。",
      items: [
        {
          question: "歌词岛支持哪些播放器？",
          answer:
            "v2.0 Beta 1 通过 Windows SMTC 支持 Apple Music、QQ 音乐、网易云音乐、酷狗、Spotify、酷我以及通用兼容播放器。"
        },
        {
          question: "歌词和翻译会上传到云端吗？",
          answer:
            "应用会从已支持的歌词服务检索内容，并在本机按歌曲维度缓存。歌词岛本身不提供账号或云端布局同步。"
        },
        {
          question: "它会自己翻译歌词吗？",
          answer:
            "不会。歌词岛只显示歌词来源已经提供的同步歌词和翻译，不自行生成机器翻译。"
        },
        {
          question: "如何打开设置？",
          answer:
            "右键单击歌词岛即可打开偏好设置，调整显示、屏幕位置、播放器锁定、模块布局和鼠标避让效果。"
        }
      ]
    },
    closing: {
      eyebrow: "v2.0 Beta 1",
      title: "让音乐在边缘陪伴，而不是占据注意力。",
      body: "在 GitHub 查看 v1.0 与源码；软件下载请前往 Microsoft Store。",
      button: "查看 v1.0 与源码",
      communityButton: "参加用户激励计划"
    },
    footer: {
      title: "歌词一直在，桌面依然属于你。",
      product: "产品",
      productLinks: [
        { label: "核心体验", href: "#experience" },
        { label: "模块化布局", href: "#modules" },
        { label: "播放器支持", href: "#players" }
      ],
      resources: "资源",
      resourceLinks: [
        { label: "Microsoft Store ↗", href: microsoftStoreUrl },
        { label: "GitHub：v1.0 与源码 ↗", href: "https://github.com/BochengYao/AppleMusicDesktopLyrics" },
        { label: "更新内容", href: "/updates" },
        { label: "用户激励计划", href: "/incentives" },
        { label: "English", href: "/en" }
      ],
      note: "播放器与音乐服务名称及商标归各自权利人所有。",
      copyright: "© 2026 Lyric Island"
    }
  },
  en: {
    languageName: "中文",
    languageHref: "/",
    navLabel: "Primary navigation",
    menuLabel: "Open navigation",
    nav: [
      { label: "Home", href: "#main" },
      { label: "What's new", href: "/en/updates", kind: "feature" },
      { label: "Community rewards", href: "/en/incentives" },
      {
        label: "Microsoft Store",
        href: microsoftStoreUrl,
        external: true,
        kind: "store"
      }
    ],
    eyebrow: "Windows desktop lyrics companion",
    heroTitle: "Lyrics, quietly above your work.",
    heroBody:
      "It glides in from the top edge when music plays and fully retreats when the room goes quiet. Lyric Island keeps synced lyrics within sight, without pulling focus away from your work.",
    storeLabel: "Microsoft Store stable",
    exploreLabel: "See how it works",
    heroImageAlt: "Lyric Island shown at the top of a Windows desktop",
    heroBadge: "v2.0 Beta 1",
    heroIslandTitle: "City lights above the screen",
    heroIslandSub: "城市光停在屏幕边缘",
    experience: {
      eyebrow: "There when it matters",
      title: "An island that knows when to surface.",
      body:
        "Lyric Island does not claim the taskbar or add another window to manage. It follows playback and softly yields nearby content when your pointer approaches.",
      watermark: "QUIETLY ABOVE",
      items: [
        {
          tag: "Playback",
          title: "Music starts. Lyrics glide into view.",
          body: "It reads the current Windows media session and enters from the top edge.",
          image: "/images/product-hero.png",
          imageAlt: "Lyric Island visible while music is playing",
          imagePosition: "72% 8%"
        },
        {
          tag: "Idle",
          title: "Playback stops. The island leaves no trace.",
          body: "With nothing playing, the desktop returns to its quiet, uninterrupted state.",
          image: "/images/product-focus.png",
          imageAlt: "Lyric Island above a focused workspace",
          imagePosition: "50% 5%"
        },
        {
          tag: "Awareness",
          title: "Move closer. Only the nearby area softens.",
          body: "Local background and text opacity fall around the pointer, keeping content underneath readable and clickable.",
          image: "/images/product-modules.png",
          imageAlt: "Lyric Island coexisting with a Windows desktop",
          imagePosition: "12% 52%"
        }
      ]
    },
    demo: {
      eyebrow: "Try the behavior",
      title: "Let the island respond to your state.",
      body:
        "Switch playback, idle, pointer proximity, and layout modes. This browser-only demo never connects to your player.",
      playbackLabel: "Playback state",
      layoutLabel: "Layout mode",
      playing: "Playing",
      idle: "Idle",
      near: "Pointer nearby",
      layoutA: "A horizontal blocks",
      layoutC: "C dual-state",
      nowPlaying: "Now playing",
      track: "Quiet Orbit",
      artist: "Lyric Island",
      lyric: "City lights rest above the screen",
      translation: "城市灯光停在屏幕边缘",
      statusPlaying: "The island is in view",
      statusIdle: "The island has retracted",
      statusNear: "Mouse-aware transparency is on",
      statusA: "Layout A horizontal blocks is active",
      statusC: "Layout C dual-state is active"
    },
    modules: {
      eyebrow: "Compose your own",
      title: "More than lyrics. A music heads-up display shaped by you.",
      body:
        "v2.0 Beta 1 breaks the island into reusable modules. A and C layouts are saved independently, while C expands on hover.",
      editorNote:
        "Drag modules onto the real island in Settings, with 18 px snapping, reordering, save, and cancel flows.",
      names: ["Album art", "Synced lyrics", "Playback controls", "Track info", "Progress", "Divider"]
    },
    compatibility: {
      eyebrow: "Follow the sound",
      title: "One island for multiple Windows players.",
      body:
        "Lyric Island reads the current media session through Windows SMTC. Follow the most recently active player or lock one in Settings.",
      note: "Available controls and timeline quality depend on the information each player exposes to Windows.",
      players: sharedPlayers
    },
    sources: {
      eyebrow: "Where lyrics come from",
      title: "Multiple providers, with lyrics and translations left intact.",
      body:
        "Supports LRCLIB, QQ Music, KuGou, NetEase, and fallback matching. It prefers source-provided synced lyrics and translations and does not generate machine translations.",
      facts: [
        { value: "4+", label: "lyric providers", detail: "Fallback matching when a preferred source does not fit" },
        { value: "LRU", label: "local cache", detail: "A configurable song-level cache" },
        { value: "1", label: "running instance", detail: "Repeat launches return to the existing app" }
      ]
    },
    faq: {
      eyebrow: "Frequently asked",
      title: "A few things to know before you begin.",
      items: [
        {
          question: "Which players does Lyric Island support?",
          answer:
            "v2.0 Beta 1 uses Windows SMTC with Apple Music, QQ Music, NetEase, KuGou, Spotify, KuWo, and generic compatible players."
        },
        {
          question: "Are lyrics and translations uploaded to the cloud?",
          answer:
            "The app retrieves content from supported lyric services and keeps a song-level cache on your device. Lyric Island does not provide accounts or cloud layout sync."
        },
        {
          question: "Does it translate lyrics on its own?",
          answer:
            "No. Lyric Island displays synced lyrics and translations already provided by a source. It does not generate machine translations."
        },
        {
          question: "How do I open Settings?",
          answer:
            "Right-click the island to adjust display, monitor placement, player lock, module layout, and mouse-aware transparency."
        }
      ]
    },
    closing: {
      eyebrow: "v2.0 Beta 1",
      title: "Let music stay at the edge, not at the center of your attention.",
      body: "View v1.0 and the source code on GitHub. Download the app from Microsoft Store.",
      button: "View v1.0 & source",
      communityButton: "Join community rewards"
    },
    footer: {
      title: "The lyrics stay. Your desktop remains yours.",
      product: "Product",
      productLinks: [
        { label: "Experience", href: "#experience" },
        { label: "Modular layouts", href: "#modules" },
        { label: "Player support", href: "#players" }
      ],
      resources: "Resources",
      resourceLinks: [
        { label: "Microsoft Store ↗", href: microsoftStoreUrl },
        { label: "GitHub: v1.0 & source ↗", href: "https://github.com/BochengYao/AppleMusicDesktopLyrics" },
        { label: "Updates", href: "/en/updates" },
        { label: "Community rewards", href: "/en/incentives" },
        { label: "中文", href: "/" }
      ],
      note: "Player and music-service names and trademarks belong to their respective owners.",
      copyright: "© 2026 Lyric Island"
    }
  }
};
