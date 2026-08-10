import { normalizeBrandCopy } from "@/lib/brand";

export type Locale = "zh" | "zhHant" | "en" | "ja";
export type LocalizedPage = "home" | "updates" | "incentives";

export const localeDetails: Record<Locale, { label: string; languageTag: string }> = {
  zh: { label: "简体中文", languageTag: "zh-CN" },
  zhHant: { label: "繁體中文", languageTag: "zh-Hant" },
  en: { label: "English", languageTag: "en" },
  ja: { label: "日本語", languageTag: "ja" }
};

export function localePath(locale: Locale, page: LocalizedPage): string {
  const prefix = locale === "zh" ? "" : locale === "zhHant" ? "/zh-hant" : locale === "ja" ? "/ja" : "/en";
  const suffix = page === "home" ? "" : `/${page}`;
  return `${prefix}${suffix || "/"}`;
}

export function isChineseLocale(locale: Locale) {
  return locale === "zh" || locale === "zhHant";
}

export function contentLocale(locale: Locale): "zh" | "en" {
  return isChineseLocale(locale) ? "zh" : "en";
}

export function displayBrand(locale: Locale) {
  return locale === "zh" ? "歌词岛" : locale === "zhHant" ? "歌詞島" : "LyricHover";
}

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

const rawCopyByLocale: Record<"zh" | "en", SiteCopy> = {
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
    heroImageAlt: "LyricHover显示在 Windows 桌面顶部",
    experience: {
      eyebrow: "",
      title: "像一座岛, 随音乐浮现,\n 也随安静隐去。",
      body:
        "不占任务栏，也不带来多余窗口。音乐响起，它自然浮现；播放暂停，它悄然收起。鼠标靠近时，又会轻轻淡开，把屏幕还给眼前的内容。",
      watermark: "LYRIC HOVER",
      items: [
        {
          tag: "播放",
          title: "一开场，就在场",
          body: "音乐响起，歌词随即从屏幕顶部自然浮现。",
          image: "/images/experience-playback.jpg",
          imageAlt: "音乐播放时浮现在屏幕顶部的LyricHover",
          imagePosition: "50% 50%"
        },
        {
          tag: "空闲",
          title: "不播放，不打扰",
          body: "没有内容正在播放时，歌词自动收回屏幕之外，让桌面恢复原本的安静。",
          image: "/images/experience-idle.png",
          imageAlt: "没有播放内容时收回屏幕外的LyricHover",
          imagePosition: "50% 50%"
        },
        {
          tag: "避让",
          title: "鼠标靠近，内容仍是主角",
          body: "鼠标经过的地方，歌词会自然变淡，让下方内容保持可读、可操作。",
          image: "/images/experience-pointer.png",
          imageAlt: "鼠标靠近时淡化避让的LyricHover",
          imagePosition: "50% 50%"
        }
      ]
    },
    demo: {
      eyebrow: "亲手体验",
      title: "动一动鼠标，\n看这座岛如何回应。",
      body:
        "切换不同状态与布局，体验LyricHover的浮现、收起与主动避让。所有操作均为浏览器演示，不会连接你的播放器。",
      playbackLabel: "播放状态",
      layoutLabel: "布局模式",
      playing: "播放",
      idle: "空闲",
      near: "鼠标靠近",
      layoutA: "水平积木",
      layoutC: "自动折叠",
      nowPlaying: "正在播放",
      track: "Quiet Orbit",
      artist: "LyricHover",
      lyric: "城市灯光停在屏幕边缘",
      translation: "City lights rest above the screen",
      statusPlaying: "LyricHover已滑入",
      statusIdle: "LyricHover已收起",
      statusNear: "鼠标避让已开启",
      statusA: "当前为 A 横向积木布局",
      statusC: "当前为 C 自动折叠布局"
    },
    modules: {
      eyebrow: "由你组合",
      title: "想怎么展开，\n就怎么呈现。",
      body:
        "水平排列，简洁舒展；自动折叠，节省空间。LyricHover会随你的布局自然变化。",
      names: ["专辑封面", "同步歌词", "播放控制", "歌曲信息", "播放进度", "分割线"],
      imageAlt: "LyricHover在三种桌面场景中的布局效果"
    },
    compatibility: {
      title: "换个播放器，\n歌词照常在场。",
      body:
        "LyricHover会自动识别当前正在使用的播放器，并随播放状态切换。你也可以在设置中锁定常用播放器，让每次播放都保持一致。",
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
          question: "LyricHover支持哪些音乐播放器？",
          answer:
            "LyricHover支持 Apple Music、网易云音乐、QQ 音乐、酷狗音乐、酷我音乐等常见播放器。只要播放器接入了 Windows SMTC 媒体控制协议，LyricHover通常都能自动识别正在播放的歌曲。\n需要注意的是，网易云音乐对 Windows SMTC 的支持并不完整。在播放器中手动拖动播放进度时，歌词进度可能无法立即同步。"
        },
        {
          question: "LyricHover免费吗？需要登录或订阅吗？",
          answer:
            "LyricHover的主体功能永久免费，无需登录LyricHover账号，也不要求订阅。\n你可以自愿加入 Pro 支持计划，支持软件继续开发，并抢先体验部分新功能。参与用户激励计划、提交有效问题或 Bug，也有机会获得 Pro 支持计划礼品码。\n Pro 权益与下载LyricHover时使用的 Microsoft Store 账户绑定。"
        },
        {
          question: "安装后如何开始使用？需要手动连接播放器吗？",
          answer:
            "不需要手动连接。\n首次打开LyricHover时，教学模式会引导你完成基本设置。之后只需打开LyricHover并播放音乐，它便会自动识别当前播放器并开始匹配歌词。"
        },
        {
          question: "播放音乐后没有显示歌词，或歌词匹配错误怎么办？",
          answer:
            "LyricHover会从多个歌词来源中自动查找并匹配歌词，以尽可能减少歌词缺失或匹配错误的情况。后续版本也会继续接入更多歌词来源，进一步提升匹配范围和准确性。"
        }
      ]
    },
    closing: {
      eyebrow: "LyricHover V2.0",
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
        { label: "GitHub：v1.0 与源码", href: "https://github.com/BochengYao/LyricHover" },
        { label: "更新内容", href: "/updates" },
        { label: "用户激励计划", href: "/incentives" },
        //{ label: "English", href: "/en" }
      ],
      note: "播放器与音乐服务名称及商标归各自权利人所有。",
      copyright: "© 2026 LyricHover"
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
    heroImageAlt: "LyricHover shown at the top of a Windows desktop",
    experience: {
      eyebrow: "",
      title: "With music, it appears.\nWith silence, it disappears.",
      body:
        "No taskbar space. No extra window. It surfaces when music starts, retreats when playback pauses, and gently fades as your pointer approaches—giving the screen back to what matters.",
      watermark: "LYRIC HOVER",
      items: [
        {
          tag: "Playback",
          title: "Music plays. Lyrics stay.",
          body: "As the music begins, lyrics naturally surface from the top of the screen.",
          image: "/images/experience-playback.jpg",
          imageAlt: "LyricHover visible at the top while music is playing",
          imagePosition: "50% 50%"
        },
        {
          tag: "Idle",
          title: "No sound. Not around.",
          body: "When nothing is playing, the lyrics retreat beyond the screen and your desktop returns to quiet.",
          image: "/images/experience-idle.png",
          imageAlt: "LyricHover retracted while nothing is playing",
          imagePosition: "50% 50%"
        },
        {
          tag: "Awareness",
          title: "Move near. Work stays clear.",
          body: "Wherever your pointer passes, the lyrics gently fade so the content beneath stays readable and clickable.",
          image: "/images/experience-pointer.png",
          imageAlt: "LyricHover fading as the pointer approaches",
          imagePosition: "50% 50%"
        }
      ]
    },
    demo: {
      eyebrow: "Try it yourself",
      title: "Move. Switch. Play.\nWatch hover sway.",
      body:
        "Switch between states and layouts to experience how lyrics surface, retreat, and move out of your way. Everything here runs as a browser demo and never connects to your player.",
      playbackLabel: "Playback state",
      layoutLabel: "Layout mode",
      playing: "Playing",
      idle: "Idle",
      near: "Pointer nearby",
      layoutA: "Horizontal blocks",
      layoutC: "Auto-collapse",
      nowPlaying: "Now playing",
      track: "Quiet Orbit",
      artist: "LyricHover",
      lyric: "City lights rest above the screen",
      translation: "城市灯光停在屏幕边缘",
      statusPlaying: "Lyrics are in view",
      statusIdle: "Lyrics have retracted",
      statusNear: "Mouse-aware transparency is on",
      statusA: "Layout A horizontal blocks is active",
      statusC: "Layout C auto-collapse is active"
    },
    modules: {
      eyebrow: "Compose your own",
      title: "How it unfolds\nis yours to mold.",
      body:
        "Horizontal when you want room to breathe. Auto-collapsed when space matters. LyricHover naturally adapts to the layout you choose.",
      names: ["Album art", "Synced lyrics", "Playback controls", "Track info", "Progress", "Divider"],
      imageAlt: "LyricHover layouts shown across three desktop scenes"
    },
    compatibility: {
      title: "Players may change.\nLyrics stay the same.",
      body:
        "LyricHover automatically recognizes the player in use and follows its playback state. You can also lock in a favorite player from Settings for a consistent experience every time.",
      note:
        "*Due to interface limitations, NetEase Cloud Music does not currently support progress-bar sync or real-time lyric resync after seeking.",
      players: sharedPlayers
    },
    sources: {
      eyebrow: "Where lyrics come from",
      title: "Many sources.\nOne perfect match.",
      body:
        "LyricHover searches providers including LRCLIB, Tencent Music, and NetEase Cloud Music to find synced lyrics and available translations for the song playing now.",
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
          question: "Which music players does LyricHover support?",
          answer:
            "LyricHover supports popular players including Apple Music, NetEase Cloud Music, QQ Music, Kugou Music, and Kuwo Music. If a player connects to the Windows SMTC media-control protocol, LyricHover can usually recognize the song automatically.\nPlease note that NetEase Cloud Music has incomplete Windows SMTC support. If you seek manually within the player, lyric progress may not update immediately."
        },
        {
          question: "Is LyricHover free? Do I need an account or subscription?",
          answer:
            "LyricHover's core features are free forever. No LyricHover account or subscription is required.\nYou can optionally join the Pro Support Plan to help fund continued development and get early access to selected new features. You may also earn a Pro Support Plan gift code by joining the Community Rewards Program and submitting a valid issue or bug report.\nPro benefits are linked to the Microsoft Store account used to download LyricHover."
        },
        {
          question: "How do I get started after installing? Do I need to connect a player?",
          answer:
            "No manual connection is needed.\nThe first time you open LyricHover, a guided tutorial walks you through the essentials. After that, simply keep LyricHover open and play some music—it will recognize the current player and start matching lyrics automatically."
        },
        {
          question: "What if lyrics do not appear, or the wrong lyrics are matched?",
          answer:
            "LyricHover searches and matches across multiple lyric sources to reduce missing or incorrect results. Future releases will continue adding sources to improve both coverage and accuracy."
        }
      ]
    },
    closing: {
      eyebrow: "LyricHover V2.0",
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
        { label: "GitHub: v1.0 & source", href: "https://github.com/BochengYao/LyricHover" },
        { label: "Updates", href: "/en/updates" },
        { label: "Community rewards", href: "/en/incentives" },
        // { label: "中文", href: "/" }
      ],
      note: "Player and music-service names and trademarks belong to their respective owners.",
      copyright: "© 2026 LyricHover"
    }
  }
};

const traditionalCharacters: Record<string, string> = {
  这: "這", 个: "個", 见: "見", 音: "音", 乐: "樂", 词: "詞", 从: "從", 顶: "頂", 结: "結", 束: "束", 视: "視", 线: "線", 边: "邊", 断: "斷", 带: "帶", 来: "來", 余: "餘", 标: "標", 轻: "輕", 还: "還", 给: "給", 内: "內", 容: "容", 开: "開", 场: "場", 闲: "閒", 时: "時", 动: "動", 让: "讓", 桌: "桌", 处: "處", 应: "應", 亲: "親", 手: "手", 体: "體", 验: "驗", 换: "換", 状: "狀", 态: "態", 布: "佈", 局: "局", 浏: "瀏", 览: "覽", 器: "器", 连: "連", 接: "接", 组: "組", 合: "合", 展: "展", 现: "現", 简: "簡", 洁: "潔", 节: "節", 间: "間", 会: "會", 随: "隨", 变: "變", 识: "識", 别: "別", 当: "當", 前: "前", 设: "設", 置: "置", 锁: "鎖", 常: "常", 用: "用", 保: "保", 持: "持", 一: "一", 致: "致", 受: "受", 限: "限", 网: "網", 易: "易", 云: "雲", 暂: "暫", 不: "不", 支: "支", 进: "進", 度: "度", 条: "條", 实: "實", 匹: "匹", 配: "配", 翻: "翻", 译: "譯", 源: "源", 广: "廣", 告: "告", 扰: "擾", 纯: "純", 粹: "粹", 问: "問", 题: "題", 知: "知", 道: "道", 哪: "哪", 些: "些", 需: "需", 要: "要", 登: "登", 录: "錄", 订: "訂", 阅: "閱", 账: "帳", 号: "號", 参: "參", 与: "與", 获: "獲", 礼: "禮", 码: "碼", 权: "權", 益: "益", 绑: "綁", 定: "定", 教: "教", 学: "學", 导: "導", 完: "完", 成: "成", 后: "後", 只: "只", 自: "自", 查: "查", 找: "找", 尽: "盡", 减: "減", 少: "少", 错: "錯", 误: "誤", 继: "繼", 续: "續", 提: "提", 升: "升", 围: "圍", 准: "準", 确: "確", 下: "下", 载: "載", 软: "軟", 件: "件", 资: "資", 评: "評", 计: "計", 划: "劃", 各: "各", 属: "屬", 于: "於", 利: "利", 人: "人", 所: "所", 有: "有"
};

function transformCopy<T>(value: T, transform: (text: string) => string): T {
  if (typeof value === "string") return transform(value) as T;
  if (Array.isArray(value)) return value.map((item) => transformCopy(item, transform)) as T;
  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value as Record<string, unknown>).map(([key, item]) => [key, transformCopy(item, transform)])) as T;
  }
  return value;
}

const traditionalSiteCopy = {
  ...transformCopy(rawCopyByLocale.zh, (text) => Array.from(text, (character) => traditionalCharacters[character] ?? character).join("")),
  nav: [
    { label: "首頁", href: "#main" },
    { label: "新功能", href: localePath("zhHant", "updates") },
    { label: "使用者激勵計畫", href: localePath("zhHant", "incentives"), kind: "feature" as const },
    { label: "Microsoft Store", href: microsoftStoreUrl, external: true, kind: "store" as const }
  ]
} satisfies SiteCopy;

const japaneseSiteCopy: SiteCopy = {
  languageName: "日本語", languageHref: "/ja", navLabel: "メインナビゲーション", menuLabel: "ナビゲーションを開く",
  nav: [
    { label: "ホーム", href: "#main" },
    { label: "新機能", href: "/ja/updates" },
    { label: "コミュニティ特典", href: "/ja/incentives", kind: "feature" },
    { label: "Microsoft Store", href: microsoftStoreUrl, external: true, kind: "store" }
  ],
  heroTitle: "この一行を、\n見える場所へ。",
  heroBody: "音楽が始まると、歌詞は画面上部に自然に現れます。再生が終われば、すっと引き上げる。いつも視界の端にいて、仕事の邪魔はしません。",
  storeLabel: "Microsoft Store から入手", exploreLabel: "仕組みを見る", heroImageAlt: "Windows デスクトップ上部に表示された LyricHover",
  experience: {
    eyebrow: "", title: "音とともに現れ、\n静けさとともに消える。", body: "タスクバーも、余計なウィンドウも不要です。音楽が鳴れば現れ、止まれば静かに退く。ポインターが近づけば淡くなり、画面をあなたの作業へ返します。", watermark: "LYRIC HOVER",
    items: [
      { tag: "再生", title: "音が鳴れば、歌詞がいる。", body: "音楽が始まると、歌詞は画面上部に自然に現れます。", image: "/images/experience-playback.jpg", imageAlt: "再生中に画面上部へ現れる LyricHover", imagePosition: "50% 50%" },
      { tag: "待機", title: "音がないなら、そっと退く。", body: "再生中のコンテンツがなければ、歌詞は画面外へ戻り、デスクトップは静けさを取り戻します。", image: "/images/experience-idle.png", imageAlt: "再生していない時に画面外へ退いた LyricHover", imagePosition: "50% 50%" },
      { tag: "配慮", title: "近づいても、作業はクリア。", body: "ポインターが通る場所では歌詞が淡くなり、下のコンテンツを読み、操作できます。", image: "/images/experience-pointer.png", imageAlt: "ポインターに合わせて淡くなる LyricHover", imagePosition: "50% 50%" }
    ]
  },
  demo: { eyebrow: "試してみる", title: "マウスを動かして、\n島の応答を見る。", body: "状態とレイアウトを切り替え、LyricHover の表示、収納、マウス回避を体験できます。これはブラウザー上のデモで、プレーヤーには接続しません。", playbackLabel: "再生状態", layoutLabel: "レイアウト", playing: "再生中", idle: "待機", near: "ポインターが近い", layoutA: "横並び", layoutC: "自動収納", nowPlaying: "再生中", track: "Quiet Orbit", artist: "LyricHover", lyric: "街の灯りが画面の端にとどまる", translation: "City lights rest above the screen", statusPlaying: "LyricHover を表示中", statusIdle: "LyricHover を収納しました", statusNear: "マウス回避が有効です", statusA: "A 横並びレイアウトを表示中", statusC: "C 自動収納レイアウトを表示中" },
  modules: { eyebrow: "自分で組み立てる", title: "どう広げるか、\nどう見せるか。", body: "横並びならのびやかに。自動収納なら省スペースに。LyricHover は選んだレイアウトに自然になじみます。", names: ["アルバムアート", "同期歌詞", "再生コントロール", "曲情報", "再生位置", "区切り線"], imageAlt: "3 つのデスクトップ場面での LyricHover レイアウト" },
  compatibility: { title: "プレーヤーが変わっても、\n歌詞はそのまま。", body: "LyricHover は使用中のプレーヤーを自動で認識し、再生状態に合わせます。設定でよく使うプレーヤーを固定すれば、いつでも一貫した体験です。", note: "*インターフェースの制限により、NetEase Cloud Music は現在、シーク後の進捗同期と歌詞の即時同期に対応していません。", players: sharedPlayers },
  sources: { eyebrow: "歌詞はどこから", title: "複数のソースを、\n一度にマッチ。", body: "LRCLIB、Tencent Music、NetEase Cloud Music などの歌詞ソースを検索し、再生中の曲に同期歌詞と翻訳を見つけます。", facts: [{ value: "4+", label: "歌詞ソース", detail: "複数のソースを自動で照合します。" }, { value: "6+", label: "主要プレーヤー", detail: "Apple Music、QQ Music、NetEase Cloud Music* などに対応。" }, { value: "0", label: "広告の中断", detail: "広告なし。歌詞だけに集中できます。" }], note: "*API の制限により、NetEase Cloud Music は進捗バー同期とシーク後のリアルタイム歌詞同期に対応していません。" },
  faq: { eyebrow: "よくある質問", title: "始める前に、\n知っておきたいこと。", items: [
    { question: "LyricHover はどの音楽プレーヤーに対応していますか？", answer: "LyricHover は Apple Music、NetEase Cloud Music、QQ Music、Kugou Music、Kuwo Music などに対応します。Windows SMTC メディア制御プロトコルに接続するプレーヤーなら、再生中の曲を通常は自動で認識します。\nNetEase Cloud Music の Windows SMTC 対応は完全ではありません。プレーヤー内で手動シークすると、歌詞の位置がすぐに更新されない場合があります。" },
    { question: "LyricHover は無料ですか？ アカウントやサブスクリプションは必要ですか？", answer: "LyricHover の基本機能はずっと無料です。LyricHover のアカウントもサブスクリプションも必要ありません。\nPro サポートプランに参加すると、開発を支援し、一部の新機能を先行体験できます。コミュニティ特典で有効な提案や不具合を送ると、Pro サポートプランのギフトコードを受け取れる場合があります。\nPro 特典は、LyricHover をダウンロードした Microsoft Store アカウントに紐づきます。" },
    { question: "インストール後はどう始めますか？ プレーヤーを手動接続する必要がありますか？", answer: "手動接続は不要です。\n初回起動時はガイドが基本設定を案内します。その後は LyricHover を開いて音楽を再生するだけで、現在のプレーヤーを認識し、歌詞の照合を始めます。" },
    { question: "歌詞が表示されない、または間違った歌詞が表示される場合は？", answer: "LyricHover は複数の歌詞ソースから自動で検索・照合し、未表示や誤一致を減らします。今後もソースを追加して、範囲と精度を高めます。" }
  ] },
  closing: { eyebrow: "LyricHover V2.0", title: "すべての一行を。\nちょうど、今に。", body: "GitHub で v1.0 とソースコードを確認できます。アプリは Microsoft Store から入手してください。", button: "GitHub", storeButton: "Microsoft Store" },
  footer: { title: "音楽が鳴れば、\n歌詞が現れる。", product: "見どころ", productLinks: [{ label: "コア体験", href: "#experience" }, { label: "モジュールレイアウト", href: "#modules" }, { label: "プレーヤー対応", href: "#players" }], resources: "リソース", resourceLinks: [{ label: "Microsoft Store", href: microsoftStoreUrl }, { label: "GitHub: v1.0 とソース", href: "https://github.com/BochengYao/LyricHover" }, { label: "更新内容", href: "/ja/updates" }, { label: "コミュニティ特典", href: "/ja/incentives" }], note: "プレーヤー名と音楽サービス名、および商標は各権利者に帰属します。", copyright: "© 2026 LyricHover" }
};

export const copyByLocale: Record<Locale, SiteCopy> = {
  zh: normalizeBrandCopy(rawCopyByLocale.zh, "zh"),
  zhHant: traditionalSiteCopy,
  en: normalizeBrandCopy(rawCopyByLocale.en, "en"),
  ja: japaneseSiteCopy
};
