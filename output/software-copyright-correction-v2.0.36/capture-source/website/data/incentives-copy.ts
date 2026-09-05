import type { Locale } from "@/data/site-copy";

export type IncentivesCopy = {
  pageTitle: string;
  pageDescription: string;
  navLabel: string;
  backLabel: string;
  languageName: string;
  languageHref: string;
  eyebrow: string;
  title: string;
  intro: string;
  privacyNote: string;
  tabs: { feature: string; bug: string };
  feature: {
    eyebrow: string;
    title: string;
    body: string;
    reward: string;
    acceptedTitle: string;
    acceptedSubtitle: string;
    acceptedEmpty: string;
  };
  bug: {
    eyebrow: string;
    title: string;
    body: string;
    reward: string;
  };
  form: {
    nickname: string;
    email: string;
    title: string;
    featureTitlePlaceholder: string;
    bugTitlePlaceholder: string;
    description: string;
    featureDescriptionPlaceholder: string;
    bugDescriptionPlaceholder: string;
    attachments: string;
    attachmentHint: string;
    identityHint: string;
    submitFeature: string;
    submitBug: string;
    submitting: string;
    successFeature: string;
    successBug: string;
    removeAttachment: string;
  };
  preview: {
    eyebrow: string;
    title: string;
    body: string;
    empty: string;
    target: string;
  };
  footerNote: string;
};

export const incentivesByLocale: Record<Locale, IncentivesCopy> = {
  zh: {
    pageTitle: "用户激励计划",
    pageDescription: "向歌词岛提交新功能建议或 Bug，查看已采纳建议与版本预告。",
    navLabel: "用户激励计划导航",
    backLabel: "返回官网",
    languageName: "EN",
    languageHref: "/en/incentives",
    eyebrow: "一起把歌词岛做得更好",
    title: "你的想法\n值得被听到",
    intro:
      "在这里提出新功能或报告 Bug。我们会在后台逐条审阅；采纳后会通过邮箱联系并发放对应奖励。",
    privacyNote: "昵称与邮箱只用于确认提交和发放奖励。",
    tabs: { feature: "新功能提议", bug: "Bug 提交" },
    feature: {
      eyebrow: "新功能提议",
      title: "如果它让歌词岛更好，我们请你收下 3 元红包。",
      body: "说清楚使用场景、目前的不便，以及你希望歌词岛如何回应。越具体的建议，越容易进入评估。",
      reward: "建议被正式采纳后，发送 3 元红包",
      acceptedTitle: "已经被听见...",
      acceptedSubtitle: "给你认为可行的方案点赞，开发者会优先处理哦",
      acceptedEmpty: "第一批被采纳的建议将在这里滚动出现。"
    },
    bug: {
      eyebrow: "Bug 提交",
      title: "帮我们找到问题，采纳后送你软件礼品码。",
      body: "请描述出现问题前做了什么、实际结果和预期结果；截图或短视频会大幅提高定位速度。",
      reward: "问题被确认并采纳后，发放软件礼品码"
    },
    form: {
      nickname: "昵称",
      email: "邮箱",
      title: "一句话标题",
      featureTitlePlaceholder: "例如：希望歌词岛支持单行/双行快速切换",
      bugTitlePlaceholder: "例如：切换播放器后歌词偶尔停住",
      description: "详细说明",
      featureDescriptionPlaceholder: "使用场景、现在遇到的问题、你理想中的交互……",
      bugDescriptionPlaceholder: "复现步骤、实际结果、预期结果、系统与播放器版本……",
      attachments: "图片或视频",
      attachmentHint: "最多 3 个文件；单个不超过 15 MB，总计不超过 30 MB。",
      identityHint: "填写一次后会在这台设备上自动带入另一类提交。",
      submitFeature: "提交新功能提议",
      submitBug: "提交 Bug",
      submitting: "正在提交…",
      successFeature: "谢谢你的提交，如果被采纳我们将通过邮件联系你❤️",
      successBug: "谢谢你的提交，如果被采纳我们将通过邮件联系你❤️",
      removeAttachment: "移除"
    },
    preview: {
      eyebrow: "版本预告",
      title: "接下来，歌词岛准备往哪里走",
      body: "这里只展示已经由维护者发布的开发预告；内容仍可能根据测试结果调整。",
      empty: "新的版本预告正在准备中。",
      target: "预计"
    },
    footerNote: "奖励由歌词岛维护者人工审核与发放；重复、无法复现或已有记录的内容可能不会重复奖励。"
  },
  en: {
    pageTitle: "Community rewards",
    pageDescription: "Suggest features, report bugs, and follow accepted ideas and release previews for Lyric Island.",
    navLabel: "Community rewards navigation",
    backLabel: "Back to the site",
    languageName: "中文",
    languageHref: "/incentives",
    eyebrow: "Help shape Lyric Island",
    title: "Your idea\ndeserves to be heard",
    intro:
      "Suggest a feature or report a bug here. We review every submission; if accepted, we will contact you by email and issue the corresponding reward.",
    privacyNote: "Your nickname and email are used only to confirm submissions and issue rewards.",
    tabs: { feature: "Feature ideas", bug: "Bug reports" },
    feature: {
      eyebrow: "Feature ideas",
      title: "If it makes Lyric Island better, we will send a small ¥3 red-packet reward.",
      body: "Describe the situation, what feels difficult today, and how you would like Lyric Island to respond. Specific ideas are easier to evaluate.",
      reward: "Accepted ideas receive a ¥3 red-packet reward",
      acceptedTitle: "Already heard...",
      acceptedSubtitle: "Like the ideas you believe in, and the developer will prioritize them.",
      acceptedEmpty: "The first accepted ideas will start orbiting here."
    },
    bug: {
      eyebrow: "Bug reports",
      title: "Help us find the issue and receive a software gift code when it is accepted.",
      body: "Tell us what you did, what happened, and what you expected. A screenshot or short video can make diagnosis much faster.",
      reward: "Confirmed and accepted reports receive a software gift code"
    },
    form: {
      nickname: "Nickname",
      email: "Email",
      title: "Short title",
      featureTitlePlaceholder: "For example: quickly switch between one and two lyric lines",
      bugTitlePlaceholder: "For example: lyrics occasionally stop after switching players",
      description: "Details",
      featureDescriptionPlaceholder: "The situation, current friction, and the interaction you have in mind…",
      bugDescriptionPlaceholder: "Steps, actual result, expected result, Windows and player version…",
      attachments: "Images or video",
      attachmentHint: "Up to 3 files; 15 MB each and 30 MB total.",
      identityHint: "Fill this in once and the other form will reuse it on this device.",
      submitFeature: "Submit feature idea",
      submitBug: "Submit bug",
      submitting: "Submitting…",
      successFeature: "Thank you. If it is accepted, we will contact you by email ❤️",
      successBug: "Thank you. If it is accepted, we will contact you by email ❤️",
      removeAttachment: "Remove"
    },
    preview: {
      eyebrow: "Release preview",
      title: "Where Lyric Island is heading next",
      body: "Only previews published by the maintainer appear here. Details may still change after testing.",
      empty: "The next release preview is being prepared.",
      target: "Target"
    },
    footerNote: "Rewards are reviewed and issued manually. Duplicate, non-reproducible, or previously reported items may not receive another reward."
  }
};
