import type { Locale } from "@/data/site-copy";
import { normalizeBrandCopy } from "@/lib/brand";

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

const rawIncentivesByLocale: Record<"zh" | "en", IncentivesCopy> = {
  zh: {
    pageTitle: "用户激励计划",
    pageDescription: "向LyricHover提交新功能建议或 Bug，查看已采纳建议与版本预告。",
    navLabel: "用户激励计划导航",
    backLabel: "返回官网",
    languageName: "EN",
    languageHref: "/en/incentives",
    eyebrow: "一起完善LyricHover",
    title: "你的建议，\n也可能成为下一次更新。",
    intro:
      "提出新功能，或告诉我们哪里还不够好。每一条反馈都会被认真查看；建议被采纳后，我们将通过邮箱与你联系，并送上相应奖励。",
    privacyNote: "昵称与邮箱仅用于核对提交信息及发放奖励。",
    tabs: { feature: "新功能提议", bug: "Bug 提交" },
    feature: {
      eyebrow: "新功能提议",
      title: "把你的想法，\n说具体一点。",
      body: "告诉我们你会在什么场景下使用它、目前哪里不够顺手，以及你期待它如何工作。信息越具体，越有助于我们评估和实现。",
      reward: "建议正式采纳后，赠送 3 元红包",
      acceptedTitle: "已经被听见...",
      acceptedSubtitle: "给你认为可行的方案点赞，开发者会优先处理哦",
      acceptedEmpty: "第一批被采纳的建议将在这里滚动出现。"
    },
    bug: {
      eyebrow: "Bug 提交",
      title: "发现问题，\n告诉我们。",
      body: "请写下问题出现前的操作、实际结果和预期结果。附上截图或短视频，能帮助我们更快定位并修复。",
      reward: "问题确认有效后，赠送软件礼品码"
    },
    form: {
      nickname: "昵称",
      email: "邮箱",
      title: "一句话标题",
      featureTitlePlaceholder: "例如：希望LyricHover支持单行/双行快速切换",
      bugTitlePlaceholder: "例如：切换播放器后歌词偶尔停住",
      description: "详细说明",
      featureDescriptionPlaceholder: "使用场景、现在遇到的问题、你理想中的交互……",
      bugDescriptionPlaceholder: "复现步骤、实际结果、预期结果、系统与播放器版本……",
      attachments: "图片或视频",
      attachmentHint: "最多 3 个文件；单个不超过 15 MB，总计不超过 30 MB。",
      identityHint: "",
      submitFeature: "提交新功能提议",
      submitBug: "提交 Bug",
      submitting: "正在提交…",
      successFeature: "谢谢你的提交，如果被采纳我们将通过邮件联系你❤️",
      successBug: "谢谢你的提交，如果被采纳我们将通过邮件联系你❤️",
      removeAttachment: "移除"
    },
    preview: {
      eyebrow: "版本预告",
      title: "下一版，\n先见一面。",
      body: "正在开发中的新功能，会在这里提前亮相。正式发布前，设计与功能仍可能随测试继续调整。",
      empty: "新的版本预告正在准备中。",
      target: "预计发布时间"
    },
    footerNote: "奖励由LyricHover维护者人工审核与发放；重复、无法复现或已有记录的内容可能不会重复奖励。"
  },
  en: {
    pageTitle: "Community rewards",
    pageDescription: "Suggest features, report bugs, and follow accepted ideas and release previews for LyricHover.",
    navLabel: "Community rewards navigation",
    backLabel: "Back to the site",
    languageName: "中文",
    languageHref: "/incentives",
    eyebrow: "Shape LyricHover with us",
    title: "Your feedback,\ncould shape the next update.",
    intro:
      "Suggest a feature, or tell us what could be better. We read every submission; if your idea is accepted, we will contact you by email and send the corresponding reward.",
    privacyNote: "Your nickname and email are used only to verify your submission and issue rewards.",
    tabs: { feature: "Feature ideas", bug: "Bug reports" },
    feature: {
      eyebrow: "Feature ideas",
      title: "Make your idea\nclear and concrete.",
      body: "Tell us where you would use it, what feels awkward today, and how you expect it to work. The more specific the details, the easier it is for us to evaluate and build.",
      reward: "Accepted ideas receive a ¥3 red-packet reward",
      acceptedTitle: "Already heard...",
      acceptedSubtitle: "Like the ideas you believe in, and the developer will prioritize them.",
      acceptedEmpty: "The first accepted ideas will start orbiting here."
    },
    bug: {
      eyebrow: "Bug reports",
      title: "Found a problem? Tell us.",
      body: "Tell us what you did before the issue appeared, what happened, and what you expected. A screenshot or short video helps us diagnose and fix it faster.",
      reward: "Verified reports receive a software gift code"
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
      identityHint: "",
      submitFeature: "Submit feature idea",
      submitBug: "Submit bug",
      submitting: "Submitting…",
      successFeature: "Thank you. If it is accepted, we will contact you by email ❤️",
      successBug: "Thank you. If it is accepted, we will contact you by email ❤️",
      removeAttachment: "Remove"
    },
    preview: {
      eyebrow: "Release preview",
      title: "Next up.\nFirst look.",
      body: "Features now in development make an early appearance here. Before release, the design and functionality may continue to evolve through testing.",
      empty: "The next release preview is being prepared.",
      target: "Expected release date"
    },
    footerNote: "Rewards are reviewed and issued manually. Duplicate, non-reproducible, or previously reported items may not receive another reward."
  }
};

export const incentivesByLocale: Record<Locale, IncentivesCopy> = {
  zh: normalizeBrandCopy(rawIncentivesByLocale.zh, "zh"),
  zhHant: {
    ...rawIncentivesByLocale.zh,
    pageTitle: "使用者激勵計畫",
    pageDescription: "向 LyricHover 提交新功能建議或 Bug，查看已採納建議與版本預告。",
    navLabel: "使用者激勵計畫導覽",
    backLabel: "返回官網",
    languageName: "繁體中文",
    languageHref: "/zh-hant/incentives",
    eyebrow: "一起讓 LyricHover 更好",
    title: "你的建議，\n也可能成為下一次更新。",
    intro: "提出新功能，或告訴我們哪裡還不夠好。每一則回饋都會被仔細閱讀；建議被採納後，我們將透過電子郵件與你聯繫，並送上相應獎勵。",
    privacyNote: "暱稱與電子郵件僅用於核對提交資訊及發放獎勵。",
    tabs: { feature: "新功能提議", bug: "Bug 提交" },
    feature: { ...rawIncentivesByLocale.zh.feature, eyebrow: "新功能提議", title: "把你的想法，\n說具體一點。", body: "告訴我們你會在什麼情境使用、目前哪裡不夠順手，以及你期待它如何運作。資訊越具體，越有助於我們評估與實現。", reward: "建議正式採納後，贈送 3 元紅包", acceptedTitle: "已經被聽見…", acceptedSubtitle: "為你認為可行的方案按讚，開發者會優先處理。", acceptedEmpty: "第一批被採納的建議將在這裡滾動出現。" },
    bug: { ...rawIncentivesByLocale.zh.bug, eyebrow: "Bug 提交", title: "發現問題，\n告訴我們。", body: "請寫下問題出現前的操作、實際結果與預期結果。附上截圖或短片，可幫助我們更快定位並修正。", reward: "問題確認有效後，贈送軟體禮品碼" },
    form: { ...rawIncentivesByLocale.zh.form, nickname: "暱稱", email: "電子郵件", title: "一句話標題", featureTitlePlaceholder: "例如：希望 LyricHover 支援單行／雙行快速切換", bugTitlePlaceholder: "例如：切換播放器後歌詞偶爾停住", description: "詳細說明", featureDescriptionPlaceholder: "使用情境、目前遇到的問題、你理想中的互動……", bugDescriptionPlaceholder: "重現步驟、實際結果、預期結果、系統與播放器版本……", attachments: "圖片或影片", attachmentHint: "最多 3 個檔案；單一不超過 15 MB，合計不超過 30 MB。", submitFeature: "提交新功能提議", submitBug: "提交 Bug", submitting: "正在提交…", successFeature: "謝謝你的提交；若被採納，我們將透過電子郵件聯繫你 ❤️", successBug: "謝謝你的提交；若被採納，我們將透過電子郵件聯繫你 ❤️", removeAttachment: "移除" },
    preview: { ...rawIncentivesByLocale.zh.preview, eyebrow: "版本預告", title: "下一版，\n先見一面。", body: "正在開發中的新功能，會在這裡提前亮相。正式發布前，設計與功能仍可能隨測試繼續調整。", empty: "新的版本預告正在準備中。", target: "預計發布時間" },
    footerNote: "獎勵由 LyricHover 維護者人工審核與發放；重複、無法重現或已有記錄的內容可能不會重複獎勵。"
  },
  en: normalizeBrandCopy(rawIncentivesByLocale.en, "en"),
  ja: {
    ...rawIncentivesByLocale.en,
    pageTitle: "コミュニティ特典",
    pageDescription: "LyricHover の機能を提案し、不具合を報告し、採用されたアイデアとリリース予定を確認できます。",
    navLabel: "コミュニティ特典のナビゲーション",
    backLabel: "サイトへ戻る",
    languageName: "日本語",
    languageHref: "/ja/incentives",
    eyebrow: "一緒に LyricHover を育てる",
    title: "あなたの声が、\n次のアップデートをつくる。",
    intro: "新機能を提案するか、もっと良くできることを教えてください。すべての投稿を読み、採用されたアイデアにはメールで連絡し、対応する特典をお送りします。",
    privacyNote: "ニックネームとメールアドレスは、投稿の確認と特典の付与にのみ使用します。",
    tabs: { feature: "機能の提案", bug: "不具合の報告" },
    feature: { eyebrow: "機能の提案", title: "アイデアを、\nもっと具体的に。", body: "使いたい場面、今の不便さ、期待する動きを教えてください。具体的なほど、評価と実装につながります。", reward: "採用された提案には ¥3 相当の特典", acceptedTitle: "もう、届いています…", acceptedSubtitle: "実現してほしい案に「いいね」を。開発者が優先して確認します。", acceptedEmpty: "最初に採用されたアイデアは、ここに流れてきます。" },
    bug: { eyebrow: "不具合の報告", title: "気づいた問題を、\n教えてください。", body: "問題が出る前の操作、実際の結果、期待した結果を書いてください。スクリーンショットや短い動画があると、より早く確認して修正できます。", reward: "有効な報告にはソフトウェアのギフトコード" },
    form: { nickname: "ニックネーム", email: "メールアドレス", title: "短いタイトル", featureTitlePlaceholder: "例：歌詞を 1 行／2 行で素早く切り替えたい", bugTitlePlaceholder: "例：プレーヤーを切り替えると歌詞が止まることがある", description: "詳しい内容", featureDescriptionPlaceholder: "使う場面、今の困りごと、理想の操作……", bugDescriptionPlaceholder: "再現手順、実際の結果、期待する結果、Windows とプレーヤーのバージョン……", attachments: "画像または動画", attachmentHint: "最大 3 ファイル、1 ファイル 15 MB、合計 30 MB まで。", identityHint: "", submitFeature: "機能を提案する", submitBug: "不具合を送る", submitting: "送信中…", successFeature: "ありがとうございます。採用された場合はメールでご連絡します ❤️", successBug: "ありがとうございます。採用された場合はメールでご連絡します ❤️", removeAttachment: "削除" },
    preview: { eyebrow: "リリース予定", title: "次の版を、\nひと足先に。", body: "開発中の新機能を、ここで先に紹介します。正式公開まで、デザインと機能はテストを通じて変わることがあります。", empty: "次のリリース予定を準備中です。", target: "公開予定日" },
    footerNote: "特典は LyricHover の管理者が手動で確認・付与します。重複、再現不能、既報の内容には追加の特典が付かない場合があります。"
  }
};
