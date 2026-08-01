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
  heroTitle: string;
  heroBody: string;
  storeLabel: string;
  exploreLabel: string;
  heroImageAlt: string;
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
    names: string[];
    imageAlt: string;
  };
  compatibility: {
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
      note: string;
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
    storeButton: string;
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
  "NetEase Cloud Music*",
  "Kugou Music",
  "Spotify",
  "Kuwo Music",
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
      { label: "新功能", href: "/updates" },
      { label: "用户激励计划", href: "/incentives", kind: "feature" },
      {
        label: "Microsoft Store",
        href: microsoftStoreUrl,
        external: true,
        kind: "store"
      }
    ],
    heroTitle: "这一句，\n值得被看见。",
    heroBody:
      "音乐响起，歌词从屏幕顶端自然浮现；播放结束，便完整收起。始终停在视线边缘，不打断眼前的工作。",
    storeLabel: "去 Microsoft Store 下载",
    exploreLabel: "看看它如何工作",
    heroImageAlt: "歌词岛显示在 Windows 桌面顶部",
    experience: {
      eyebrow: "",
      title: "像一座岛, 随音乐浮现,\n 也随安静隐去。",
      body:
        "不占任务栏，也不带来多余窗口。音乐响起，它自然浮现；播放暂停，它悄然收起。鼠标靠近时，又会轻轻淡开，把屏幕还给眼前的内容。",
      watermark: "LYRIC ISLAND",
      items: [
        {
          tag: "播放",
          title: "一开场，就在场",
          body: "音乐响起，歌词随即从屏幕顶部自然浮现。",
          image: "/images/experience-playback.jpg",
          imageAlt: "音乐播放时浮现在屏幕顶部的歌词岛",
          imagePosition: "50% 50%"
        },
        {
          tag: "空闲",
          title: "不播放，不打扰",
          body: "没有内容正在播放时，歌词自动收回屏幕之外，让桌面恢复原本的安静。",
          image: "/images/experience-idle.png",
          imageAlt: "没有播放内容时收回屏幕外的歌词岛",
          imagePosition: "50% 50%"
        },
        {
          tag: "避让",
          title: "鼠标靠近，内容仍是主角",
          body: "鼠标经过的地方，歌词会自然变淡，让下方内容保持可读、可操作。",
          image: "/images/experience-pointer.png",
          imageAlt: "鼠标靠近时淡化避让的歌词岛",
          imagePosition: "50% 50%"
        }
      ]
    },
    demo: {
      eyebrow: "亲手体验",
      title: "动一动鼠标，\n看这座岛如何回应。",
      body:
        "切换不同状态与布局，体验歌词岛的浮现、收起与主动避让。所有操作均为浏览器演示，不会连接你的播放器。",
      playbackLabel: "播放状态",
      layoutLabel: "布局模式",
      playing: "播放",
      idle: "空闲",
      near: "鼠标靠近",
      layoutA: "水平积木",
      layoutC: "自动折叠",
      nowPlaying: "正在播放",
      track: "Quiet Orbit",
      artist: "Lyric Island",
      lyric: "城市灯光停在屏幕边缘",
      translation: "City lights rest above the screen",
      statusPlaying: "歌词岛已滑入",
      statusIdle: "歌词岛已收起",
      statusNear: "鼠标避让已开启",
      statusA: "当前为 A 横向积木布局",
      statusC: "当前为 C 自动折叠布局"
    },
    modules: {
      eyebrow: "由你组合",
      title: "想怎么展开，\n就怎么呈现。",
      body:
        "水平排列，简洁舒展；自动折叠，节省空间。歌词岛会随你的布局自然变化。",
      names: ["专辑封面", "同步歌词", "播放控制", "歌曲信息", "播放进度", "分割线"],
      imageAlt: "歌词岛在三种桌面场景中的布局效果"
    },
    compatibility: {
      title: "换个播放器，\n歌词照常在场。",
      body:
        "歌词岛会自动识别当前正在使用的播放器，并随播放状态切换。你也可以在设置中锁定常用播放器，让每次播放都保持一致。",
      note: "*受接口限制，网易云音乐暂不支持进度条同步与拖动进度条后的实时歌词同步。",
      players: ["Apple Music", "QQ 音乐", "网易云音乐*", "酷狗音乐", "Spotify", "酷我音乐", "通用 SMTC"]
    },
    sources: {
      eyebrow: "歌词来自哪里",
      title: "多个来源，\n一次匹配。",
      body:
        "支持 LRCLIB、腾讯音乐和网易云音乐等歌词来源，自动为正在播放的歌曲寻找同步歌词与翻译。",
        facts: [
          { value: "4+", label: "歌词来源", detail: "多个来源自动匹配，减少歌词缺失。" },
          { value: "6+", label: "主流播放器", detail: "兼容 Apple Music、QQ 音乐、网易云音乐*等。" },
          { value: "0", label: "广告打扰", detail: "无广告，使用更纯粹" }
        ],
        note: "*受接口限制，网易云音乐暂不支持进度条同步与拖动进度条后的实时歌词同步。"
    },
    faq: {
      eyebrow: "常见问题",
      title: "开始之前\n你可能想知道",
      items: [
        {
          question: "歌词岛支持哪些音乐播放器？",
          answer:
            "歌词岛支持 Apple Music、网易云音乐、QQ 音乐、酷狗音乐、酷我音乐等常见播放器。只要播放器接入了 Windows SMTC 媒体控制协议，歌词岛通常都能自动识别正在播放的歌曲。\n需要注意的是，网易云音乐对 Windows SMTC 的支持并不完整。在播放器中手动拖动播放进度时，歌词进度可能无法立即同步。"
        },
        {
          question: "歌词岛免费吗？需要登录或订阅吗？",
          answer:
            "歌词岛的主体功能永久免费，无需登录歌词岛账号，也不要求订阅。\n你可以自愿加入 Pro 支持计划，支持软件继续开发，并抢先体验部分新功能。参与用户激励计划、提交有效问题或 Bug，也有机会获得 Pro 支持计划礼品码。\n Pro 权益与下载歌词岛时使用的 Microsoft Store 账户绑定。"
        },
        {
          question: "安装后如何开始使用？需要手动连接播放器吗？",
          answer:
            "不需要手动连接。\n首次打开歌词岛时，教学模式会引导你完成基本设置。之后只需打开歌词岛并播放音乐，它便会自动识别当前播放器并开始匹配歌词。"
        },
        {
          question: "播放音乐后没有显示歌词，或歌词匹配错误怎么办？",
          answer:
            "歌词岛会从多个歌词来源中自动查找并匹配歌词，以尽可能减少歌词缺失或匹配错误的情况。后续版本也会继续接入更多歌词来源，进一步提升匹配范围和准确性。"
        }
      ]
    },
    closing: {
      eyebrow: "歌词岛 V2.0",
      title: "每一句,  都刚好在场｡",
      body: "在 GitHub 查看 v1.0 与源码；软件下载请前往 Microsoft Store｡",
      button: "GitHub",
      storeButton: "Microsoft Store"
    },
    footer: {
      title: "音乐响起\n歌词自然浮现",
      product: "回看",
      productLinks: [
        { label: "核心体验", href: "#experience" },
        { label: "模块化布局", href: "#modules" },
        { label: "播放器支持", href: "#players" }
      ],
      resources: "资源",
      resourceLinks: [
        { label: "Microsoft Store", href: microsoftStoreUrl },
        { label: "GitHub：v1.0 与源码", href: "https://github.com/BochengYao/AppleMusicDesktopLyrics" },
        { label: "更新内容", href: "/updates" },
        { label: "用户激励计划", href: "/incentives" },
        //{ label: "English", href: "/en" }
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
      { label: "What's new", href: "/en/updates" },
      { label: "Community rewards", href: "/en/incentives", kind: "feature" },
      {
        label: "Microsoft Store",
        href: microsoftStoreUrl,
        external: true,
        kind: "store"
      }
    ],
    heroTitle: "This line\ndeserves to be seen.",
    heroBody:
      "When music starts, lyrics naturally surface from the top of your screen. When playback ends, they fully retreat—always at the edge of your vision, never in the way of your work.",
    storeLabel: "Get it from Microsoft Store",
    exploreLabel: "See how it works",
    heroImageAlt: "Lyric Island shown at the top of a Windows desktop",
    experience: {
      eyebrow: "",
      title: "With music, it appears.\nWith silence, it disappears.",
      body:
        "No taskbar space. No extra window. It surfaces when music starts, retreats when playback pauses, and gently fades as your pointer approaches—giving the screen back to what matters.",
      watermark: "LYRIC ISLAND",
      items: [
        {
          tag: "Playback",
          title: "Music plays. Lyrics stay.",
          body: "As the music begins, lyrics naturally surface from the top of the screen.",
          image: "/images/experience-playback.jpg",
          imageAlt: "Lyric Island visible at the top while music is playing",
          imagePosition: "50% 50%"
        },
        {
          tag: "Idle",
          title: "No sound. Not around.",
          body: "When nothing is playing, the lyrics retreat beyond the screen and your desktop returns to quiet.",
          image: "/images/experience-idle.png",
          imageAlt: "Lyric Island retracted while nothing is playing",
          imagePosition: "50% 50%"
        },
        {
          tag: "Awareness",
          title: "Move near. Work stays clear.",
          body: "Wherever your pointer passes, the lyrics gently fade so the content beneath stays readable and clickable.",
          image: "/images/experience-pointer.png",
          imageAlt: "Lyric Island fading as the pointer approaches",
          imagePosition: "50% 50%"
        }
      ]
    },
    demo: {
      eyebrow: "Try it yourself",
      title: "Move. Switch. Play.\nWatch the island sway.",
      body:
        "Switch between states and layouts to experience how Lyric Island surfaces, retreats, and moves out of your way. Everything here runs as a browser demo and never connects to your player.",
      playbackLabel: "Playback state",
      layoutLabel: "Layout mode",
      playing: "Playing",
      idle: "Idle",
      near: "Pointer nearby",
      layoutA: "Horizontal blocks",
      layoutC: "Auto-collapse",
      nowPlaying: "Now playing",
      track: "Quiet Orbit",
      artist: "Lyric Island",
      lyric: "City lights rest above the screen",
      translation: "城市灯光停在屏幕边缘",
      statusPlaying: "The island is in view",
      statusIdle: "The island has retracted",
      statusNear: "Mouse-aware transparency is on",
      statusA: "Layout A horizontal blocks is active",
      statusC: "Layout C auto-collapse is active"
    },
    modules: {
      eyebrow: "Compose your own",
      title: "How it unfolds\nis yours to mold.",
      body:
        "Horizontal when you want room to breathe. Auto-collapsed when space matters. Lyric Island naturally adapts to the layout you choose.",
      names: ["Album art", "Synced lyrics", "Playback controls", "Track info", "Progress", "Divider"],
      imageAlt: "Lyric Island layouts shown across three desktop scenes"
    },
    compatibility: {
      title: "Players may change.\nLyrics stay the same.",
      body:
        "Lyric Island automatically recognizes the player in use and follows its playback state. You can also lock in a favorite player from Settings for a consistent experience every time.",
      note:
        "*Due to interface limitations, NetEase Cloud Music does not currently support progress-bar sync or real-time lyric resync after seeking.",
      players: sharedPlayers
    },
    sources: {
      eyebrow: "Where lyrics come from",
      title: "Many sources.\nOne perfect match.",
      body:
        "Lyric Island searches providers including LRCLIB, Tencent Music, and NetEase Cloud Music to find synced lyrics and available translations for the song playing now.",
        facts: [
          { value: "4+", label: "lyric sources", detail: "Multiple sources match automatically, so fewer songs go without lyrics." },
          { value: "6+", label: "popular players", detail: "Works with Apple Music, QQ Music, NetEase Cloud Music*, and more." },
          { value: "0", label: "ad interruptions", detail: "No ads. Nothing between you and the lyrics." }
        ],
        note:
          "*Due to API limitations, NetEase Cloud Music currently does not support progress-bar synchronization or real-time lyric synchronization after you drag the progress bar."
    },
    faq: {
      eyebrow: "Frequently asked",
      title: "Before you begin,\nhere's what you may want to know.",
      items: [
        {
          question: "Which music players does Lyric Island support?",
          answer:
            "Lyric Island supports popular players including Apple Music, NetEase Cloud Music, QQ Music, Kugou Music, and Kuwo Music. If a player connects to the Windows SMTC media-control protocol, Lyric Island can usually recognize the song automatically.\nPlease note that NetEase Cloud Music has incomplete Windows SMTC support. If you seek manually within the player, lyric progress may not update immediately."
        },
        {
          question: "Is Lyric Island free? Do I need an account or subscription?",
          answer:
            "Lyric Island's core features are free forever. No Lyric Island account or subscription is required.\nYou can optionally join the Pro Support Plan to help fund continued development and get early access to selected new features. You may also earn a Pro Support Plan gift code by joining the Community Rewards Program and submitting a valid issue or bug report.\nPro benefits are linked to the Microsoft Store account used to download Lyric Island."
        },
        {
          question: "How do I get started after installing? Do I need to connect a player?",
          answer:
            "No manual connection is needed.\nThe first time you open Lyric Island, a guided tutorial walks you through the essentials. After that, simply keep Lyric Island open and play some music—it will recognize the current player and start matching lyrics automatically."
        },
        {
          question: "What if lyrics do not appear, or the wrong lyrics are matched?",
          answer:
            "Lyric Island searches and matches across multiple lyric sources to reduce missing or incorrect results. Future releases will continue adding sources to improve both coverage and accuracy."
        }
      ]
    },
    closing: {
      eyebrow: "Lyric Island V2.0",
      title: "Every line.\nRight on time.",
      body: "View v1.0 and the source code on GitHub. Download the app from Microsoft Store.",
      button: "GitHub",
      storeButton: "Microsoft Store"
    },
    footer: {
      title: "Music plays.\nLyrics stay.",
      product: "Look back",
      productLinks: [
        { label: "Experience", href: "#experience" },
        { label: "Modular layouts", href: "#modules" },
        { label: "Player support", href: "#players" }
      ],
      resources: "Resources",
      resourceLinks: [
        { label: "Microsoft Store", href: microsoftStoreUrl },
        { label: "GitHub: v1.0 & source", href: "https://github.com/BochengYao/AppleMusicDesktopLyrics" },
        { label: "Updates", href: "/en/updates" },
        { label: "Community rewards", href: "/en/incentives" },
        // { label: "中文", href: "/" }
      ],
      note: "Player and music-service names and trademarks belong to their respective owners.",
      copyright: "© 2026 Lyric Island"
    }
  }
};
