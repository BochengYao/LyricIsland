"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { AdminNav } from "@/components/AdminNav";
import { AdminPromoCodes } from "@/components/AdminPromoCodes";
import { ReleasePreviewProgressRing } from "@/components/ReleasePreviewProgressRing";
import { ExternalArrow } from "@/components/ExternalArrow";
import { LogoLockup } from "@/components/SitePage";
import {
  defaultFeatureContent,
  LEGACY_FEATURE_RELEASE_VERSION,
  isFeatureReleaseVersion,
  sanitizeFeatureContent
} from "@/data/feature-content";
import type {
  AccessLogEntry,
  AccessSeverity,
  FeatureContent,
  FeatureContentSection,
  IncentiveSubmission,
  ReleasePreview,
  ReleasePreviewFeature,
  RewardStatus,
  SubmissionStatus
} from "@/data/incentives-types";
import {
  normalizeReleasePreviewContent,
  RELEASE_PREVIEW_SLIDER_READY_VALUE,
  RELEASE_PREVIEW_SLIDER_TESTING_VALUE,
  splitPreviewLines
} from "@/data/release-preview-content";
import {
  formatReleaseTiming,
  releaseTimingFromTargetDate,
  targetDateForReleaseTiming,
  type ReleaseTimingPreset
} from "@/lib/release-timing";

type AuthState = "checking" | "login" | "ready";
type Panel = "submissions" | "features" | "previews" | "access" | "promo-codes";
type SaveFeedback = { tone: "pending" | "success" | "error"; message: string };
type PreviewDraft = {
  id?: string;
  version: string;
  note_zh: string;
  note_en: string;
  note_zh_tw: string;
  note_ja: string;
  features: ReleasePreviewFeature[];
  target_date: string;
  status: "draft" | "published";
};
type TranslationLocale = "en" | "zh-tw" | "ja";
type PreviewLocale = "zh" | TranslationLocale;
type LocalizedTranslations = Record<TranslationLocale, Record<string, string>>;

const translationLocales: TranslationLocale[] = ["en", "zh-tw", "ja"];
const previewLocaleOptions: Array<{ value: PreviewLocale; label: string }> = [
  { value: "zh", label: "中文" },
  { value: "en", label: "English" },
  { value: "zh-tw", label: "繁中" },
  { value: "ja", label: "日本語" }
];
const previewNoteFields = {
  zh: "note_zh",
  en: "note_en",
  "zh-tw": "note_zh_tw",
  ja: "note_ja"
} as const;
const previewFeatureFields = {
  zh: "content_zh",
  en: "content_en",
  "zh-tw": "content_zh_tw",
  ja: "content_ja"
} as const;
const releaseTimingOptions: Array<{ value: ReleaseTimingPreset; label: string }> = [
  { value: "today", label: "今天" },
  { value: "tomorrow", label: "明天" },
  { value: "this-week", label: "本周内" },
  { value: "two-weeks", label: "两周内" },
  { value: "this-month", label: "本月内" },
  { value: "custom-days", label: "自定义天数" },
  { value: "tbd", label: "待定" }
];
type BulkAction =
  | `status:${SubmissionStatus}`
  | `reward:${RewardStatus}`
  | "flag:on"
  | "flag:off"
  | "public:on"
  | "public:off"
  | "delete";

const statusLabels: Record<SubmissionStatus, string> = {
  pending: "待审阅",
  reviewing: "审阅中",
  accepted: "已采纳",
  declined: "未采纳"
};

const rewardLabels: Record<RewardStatus, string> = {
  not_eligible: "暂不发放",
  pending: "待发放",
  issued: "已发放"
};

const compactStatusLabels: Record<SubmissionStatus, string> = {
  pending: "待审",
  reviewing: "审阅",
  accepted: "采纳",
  declined: "拒绝"
};

const compactRewardLabels: Record<RewardStatus, string> = {
  not_eligible: "不发",
  pending: "待发",
  issued: "已发"
};

const emptyPreviewDraft: PreviewDraft = {
  version: "",
  note_zh: "",
  note_en: "",
  note_zh_tw: "",
  note_ja: "",
  features: [],
  target_date: "",
  status: "draft"
};

function newPreviewFeatureId() {
  return globalThis.crypto?.randomUUID?.() ?? `feature-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function newPreviewFeature(contentZh = ""): ReleasePreviewFeature {
  return {
    id: newPreviewFeatureId(),
    sort_order: 1,
    progress: 0,
    stage: "development",
    content_zh: contentZh,
    content_en: "",
    content_zh_tw: "",
    content_ja: ""
  };
}

function previewToDraft(preview: ReleasePreview): PreviewDraft {
  const content = normalizeReleasePreviewContent(preview);
  return {
    id: preview.id,
    version: preview.version,
    note_zh: content.note_zh,
    note_en: content.note_en,
    note_zh_tw: content.note_zh_tw,
    note_ja: content.note_ja,
    features: content.features.map((feature, index) => ({
      ...feature,
      id: feature.id.startsWith("legacy-") ? newPreviewFeatureId() : feature.id,
      sort_order: index + 1
    })),
    target_date: preview.target_date ?? "",
    status: preview.status
  };
}

function previewTranslationSignature(draft: PreviewDraft) {
  return JSON.stringify({
    id: draft.id ?? "",
    note_zh: draft.note_zh,
    features: draft.features.map((feature) => ({ id: feature.id, content_zh: feature.content_zh }))
  });
}

const eventLabels: Record<string, string> = {
  page_view: "页面访问",
  login_succeeded: "后台登录成功",
  login_failed: "后台登录失败",
  cross_origin_login_attempt: "跨站登录尝试",
  submission_created: "新反馈提交",
  submission_updated: "更新反馈",
  submission_deleted: "删除反馈",
  security_alerts_acknowledged: "确认安全提醒",
  unauthorized_submission_delete: "未授权删除尝试",
  unauthorized_submission_update: "未授权修改尝试",
  unauthorized_alert_acknowledge: "未授权确认提醒"
};

const auditFieldLabels: Record<string, string> = {
  kind: "反馈类型",
  nickname: "提交者昵称",
  email: "联系邮箱",
  title: "标题",
  body: "反馈内容",
  status: "审阅状态",
  reward_status: "奖励状态",
  developer_reply: "开发者回复",
  is_flagged: "红旗标注",
  is_public: "前台展示",
  like_count: "点赞数",
  created_at: "提交时间"
};

const eventDescriptions: Record<string, string> = {
  page_view: "记录一次页面访问",
  login_succeeded: "管理员密码校验通过并建立登录会话",
  login_failed: "管理员密码校验失败，未建立登录会话",
  cross_origin_login_attempt: "其他站点尝试向后台登录接口发起请求",
  security_alerts_acknowledged: "管理员已确认当前异常提醒",
  unauthorized_submission_delete: "未通过会话或来源校验的删除请求",
  unauthorized_submission_update: "未通过会话或来源校验的修改请求",
  unauthorized_alert_acknowledge: "未登录状态尝试确认异常提醒"
};

type AuditChange = { field: string; before: unknown; after: unknown };

function auditText(field: string, value: unknown) {
  if (value === null || value === undefined || value === "") return "空";
  if (field === "status" && typeof value === "string") {
    return statusLabels[value as SubmissionStatus] ?? value;
  }
  if (field === "reward_status" && typeof value === "string") {
    return rewardLabels[value as RewardStatus] ?? value;
  }
  if (field === "kind") return value === "bug" ? "Bug" : "新功能";
  if (field === "is_flagged") return value ? "已标注" : "未标注";
  if (field === "is_public") return value ? "正在展出" : "未展出";
  if (field === "created_at" && typeof value === "string") return formatDate(value);
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

function detailString(details: Record<string, unknown>, key: string) {
  return typeof details[key] === "string" ? details[key] : "";
}

function AccessEventDetail({ item }: { item: AccessLogEntry }) {
  const title = detailString(item.details, "submissionTitle");
  const kind = detailString(item.details, "submissionKind");
  const changes = Array.isArray(item.details.changes)
    ? item.details.changes.filter((change): change is AuditChange =>
        Boolean(change) && typeof change === "object" && "field" in change
      )
    : [];

  if (item.event_type === "submission_updated") {
    return (
      <div className="accessEventDetail">
        <strong>{kind === "bug" ? "Bug" : "建议"} · {title || `ID ${detailString(item.details, "submissionId").slice(0, 8)}`}</strong>
        {changes.length ? (
          <div className="auditChanges">
            {changes.map((change, index) => {
              const before = auditText(change.field, change.before);
              const after = auditText(change.field, change.after);
              return (
                <div className="auditChange" key={`${change.field}-${index}`}>
                  <b>{auditFieldLabels[change.field] ?? change.field}</b>
                  <span title={before}>{before}</span>
                  <i aria-hidden="true">→</i>
                  <span className="after" title={after}>{after}</span>
                </div>
              );
            })}
          </div>
        ) : <small>旧日志未保存字段差异；后续更新会显示修改前后的具体内容。</small>}
      </div>
    );
  }

  if (item.event_type === "submission_created") {
    return (
      <div className="accessEventDetail">
        <strong>{detailString(item.details, "kind") === "bug" ? "Bug" : "建议"} · {detailString(item.details, "title")}</strong>
        <p title={detailString(item.details, "body")}>{detailString(item.details, "body") || "未记录反馈正文"}</p>
        <small>提交者：@{detailString(item.details, "nickname") || "未知"}</small>
      </div>
    );
  }

  if (item.event_type === "submission_deleted") {
    const snapshot = item.details.snapshot && typeof item.details.snapshot === "object"
      ? item.details.snapshot as Record<string, unknown>
      : {};
    return (
      <div className="accessEventDetail">
        <strong>已删除 · {title || auditText("title", snapshot.title)}</strong>
        <p title={auditText("body", snapshot.body)}>{auditText("body", snapshot.body)}</p>
      </div>
    );
  }

  if (item.event_type === "page_view") {
    const pageTitle = accessDetailText(item.details, "page_title");
    const viewport = accessDetailText(item.details, "viewport");
    const screen = accessDetailText(item.details, "screen");
    const timezone = accessDetailText(item.details, "timezone");
    const language = accessDetailText(item.details, "language");
    return (
      <div className="accessEventDetail">
        <strong>{pageTitle || "官网页面访问"}</strong>
        <p>{[viewport && `视口 ${viewport}`, screen && `屏幕 ${screen}`, timezone, language].filter(Boolean).join(" · ") || "客户端未上报显示环境"}</p>
        {typeof item.details.authenticated === "boolean" && (
          <small>{item.details.authenticated ? "访问时后台会话有效" : "访问时未登录后台"}</small>
        )}
        <details>
          <summary>完整访问参数</summary>
          <pre style={{ whiteSpace: "pre-wrap", wordBreak: "break-all", fontSize: 11, maxWidth: 520 }}>
            {JSON.stringify({
              ip: item.ip_address,
              ip_source: item.ip_source,
              forwarded_for: item.forwarded_for,
              country: item.country,
              region: item.region,
              city: item.city,
              request_id: item.request_id,
              accept_language: item.accept_language,
              user_agent: item.user_agent,
              referrer: item.referrer,
              ...item.details
            }, null, 2)}
          </pre>
        </details>
      </div>
    );
  }

  return (
    <div className="accessEventDetail">
      <strong>{eventDescriptions[item.event_type] ?? "系统审计事件"}</strong>
    </div>
  );
}

function formatDate(value: string) {
  return new Date(value).toLocaleString("zh-CN", { hour12: false });
}

function toDateTimeLocal(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

function formatCompactDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false
  }).format(date).replace("/", "-");
}

function deviceSummary(userAgent: string | null) {
  if (!userAgent) return "未知设备";
  const browser = userAgent.includes("Edg/") ? "Edge" : userAgent.includes("Chrome/") ? "Chrome" : userAgent.includes("Firefox/") ? "Firefox" : userAgent.includes("Safari/") ? "Safari" : "其他浏览器";
  const system = userAgent.includes("Windows") ? "Windows" : userAgent.includes("Android") ? "Android" : userAgent.includes("iPhone") || userAgent.includes("iPad") ? "iOS" : userAgent.includes("Mac OS") ? "macOS" : "未知系统";
  return `${system} · ${browser}`;
}

function deviceCategory(userAgent: string | null) {
  if (!userAgent) return "unknown";
  if (/bot|crawler|spider|headless|curl|wget/i.test(userAgent)) return "bot";
  if (/Android|iPhone|iPad|Mobile/i.test(userAgent)) return "mobile";
  return "desktop";
}

function accessDetailText(details: Record<string, unknown>, key: string) {
  const value = details[key];
  return typeof value === "string" || typeof value === "number" ? String(value) : "";
}

const accessFilterInputStyle = {
  minWidth: "150px",
  minHeight: "42px",
  border: "1px solid #cbc6bf",
  borderRadius: "12px",
  padding: "8px 11px",
  font: "inherit",
  background: "#fff"
} as const;

async function fetchWithTimeout(
  input: RequestInfo | URL,
  init: RequestInit,
  timeoutMs = 15_000
) {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } finally {
    window.clearTimeout(timeout);
  }
}

export function AdminIncentives() {
  const [auth, setAuth] = useState<AuthState>("checking");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submissions, setSubmissions] = useState<IncentiveSubmission[]>([]);
  const [featureContent, setFeatureContent] = useState<FeatureContent>(defaultFeatureContent);
  const [featureSaving, setFeatureSaving] = useState(false);
  const [featureMessage, setFeatureMessage] = useState("");
  const [selectedFeatureVersion, setSelectedFeatureVersion] = useState("");
  const [previews, setPreviews] = useState<ReleasePreview[]>([]);
  const [draftPreviews, setDraftPreviews] = useState<ReleasePreview[]>([]);
  const [accessLogs, setAccessLogs] = useState<AccessLogEntry[]>([]);
  const [unreadAlerts, setUnreadAlerts] = useState(0);
  const [kindFilter, setKindFilter] = useState<"all" | "feature" | "bug">("all");
  const [statusFilter, setStatusFilter] = useState<"all" | SubmissionStatus>("all");
  const [severityFilter, setSeverityFilter] = useState<"all" | AccessSeverity>("all");
  const [scopeFilter, setScopeFilter] = useState<"all" | "public" | "admin">("all");
  const [eventFilter, setEventFilter] = useState("all");
  const [methodFilter, setMethodFilter] = useState("all");
  const [countryFilter, setCountryFilter] = useState("all");
  const [deviceFilter, setDeviceFilter] = useState<"all" | "desktop" | "mobile" | "bot" | "unknown">("all");
  const [logQuery, setLogQuery] = useState("");
  const [logDateFrom, setLogDateFrom] = useState("");
  const [logDateTo, setLogDateTo] = useState("");
  const [panel, setPanel] = useState<Panel>("submissions");
  const [viewMode, setViewMode] = useState<"table" | "cards">("table");
  const [savingId, setSavingId] = useState("");
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [bulkSaving, setBulkSaving] = useState(false);
  const [bulkMessage, setBulkMessage] = useState("");
  const [saveFeedback, setSaveFeedback] = useState<Record<string, SaveFeedback>>({});
  const [editing, setEditing] = useState<IncentiveSubmission | null>(null);
  const [previewTimingPreset, setPreviewTimingPreset] = useState<ReleaseTimingPreset>("tbd");
  const [previewCustomDays, setPreviewCustomDays] = useState(14);
  const [previewDraft, setPreviewDraft] = useState<PreviewDraft>(emptyPreviewDraft);
  const previewDraftRef = useRef(previewDraft);
  const [previewLocale, setPreviewLocale] = useState<PreviewLocale>("zh");
  const [previewBulkInput, setPreviewBulkInput] = useState("");
  const [previewSaving, setPreviewSaving] = useState(false);
  const [draftMenuOpen, setDraftMenuOpen] = useState(false);
  const [translationSaving, setTranslationSaving] = useState<"features" | "preview" | null>(null);
  const translationRequestRef = useRef<"features" | "preview" | null>(null);
  const translationLocked = translationSaving !== null;
  const previewTranslationLocked = translationLocked;

  useEffect(() => {
    previewDraftRef.current = previewDraft;
  }, [previewDraft]);

  const featureVersionOptions = useMemo(() => {
    const versions = [
      ...featureContent.sections.map((section) => section.release_version),
      ...previews.map((preview) => preview.version),
      ...draftPreviews.map((preview) => preview.version)
    ].filter((version): version is string => typeof version === "string" && Boolean(version.trim()));
    if (!versions.includes(LEGACY_FEATURE_RELEASE_VERSION)) versions.push(LEGACY_FEATURE_RELEASE_VERSION);
    return [...new Set(versions)];
  }, [draftPreviews, featureContent.sections, previews]);

  const activeFeatureVersion = featureVersionOptions.includes(selectedFeatureVersion)
    ? selectedFeatureVersion
    : (featureVersionOptions[0] ?? LEGACY_FEATURE_RELEASE_VERSION);
  const activeFeatureSections = featureContent.sections.filter((section) => section.release_version === activeFeatureVersion);

  async function loadData() {
    const [submissionResponse, featureResponse, previewResponse, logResponse] = await Promise.all([
      fetch("/api/incentives/admin/submissions", { cache: "no-store" }),
      fetch("/api/incentives/admin/features", { cache: "no-store" }),
      fetch("/api/incentives/admin/previews", { cache: "no-store" }),
      fetch("/api/incentives/admin/access-logs", { cache: "no-store" })
    ]);
    if ([submissionResponse, featureResponse, previewResponse, logResponse].some((response) => response.status === 401)) {
      setAuth("login");
      return;
    }
    if (!submissionResponse.ok || !featureResponse.ok || !previewResponse.ok) throw new Error("后台数据读取失败");
    const submissionData = (await submissionResponse.json()) as { submissions: IncentiveSubmission[] };
    const featureData = (await featureResponse.json()) as { content: unknown };
    const previewData = (await previewResponse.json()) as { previews: ReleasePreview[]; drafts?: ReleasePreview[] };
    const logData = logResponse.ok
      ? await logResponse.json() as { logs: AccessLogEntry[]; unreadAlerts: number }
      : { logs: [] as AccessLogEntry[], unreadAlerts: 0 };
    setSubmissions(submissionData.submissions);
    setFeatureContent(sanitizeFeatureContent(featureData.content));
    setPreviews(previewData.previews);
    setDraftPreviews(previewData.drafts ?? []);
    const currentPreview = previewData.previews[0] ?? previewData.drafts?.[0];
    if (currentPreview) {
      const draft = previewToDraft(currentPreview);
      setPreviewDraft(draft);
      setPreviewBulkInput(draft.features.map((feature) => feature.content_zh).join("\n"));
      const timing = releaseTimingFromTargetDate(currentPreview.target_date);
      setPreviewTimingPreset(timing.preset);
      setPreviewCustomDays(timing.days ?? 14);
    }
    setAccessLogs(logData.logs);
    setUnreadAlerts(logData.unreadAlerts);
    if (!logResponse.ok) setError("访问日志表尚未初始化；反馈管理仍可正常使用");
    setAuth("ready");
  }

  useEffect(() => {
    loadData().catch((loadError) => {
      setError(loadError instanceof Error ? loadError.message : "后台数据读取失败");
      setAuth("login");
    });
  }, []);

  async function login(event: React.FormEvent) {
    event.preventDefault();
    setError("");
    const response = await fetch("/api/incentives/admin/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ password })
    });
    const result = (await response.json()) as { error?: string };
    if (!response.ok) {
      setError(result.error ?? "登录失败");
      return;
    }
    setPassword("");
    await loadData();
  }

  async function logout() {
    await fetch("/api/incentives/admin/logout", { method: "POST" });
    setAuth("login");
  }

  function editSubmission(id: string, patch: Partial<IncentiveSubmission>, markDirty = true) {
    setSubmissions((items) => items.map((item) => item.id === id ? { ...item, ...patch } : item));
    if (markDirty) setSaveFeedback((items) => ({ ...items, [id]: { tone: "pending", message: "有未保存的更改" } }));
  }

  async function saveSubmission(item: IncentiveSubmission, patch: Partial<IncentiveSubmission> = {}) {
    const next = { ...item, ...patch };
    setSavingId(item.id);
    setError("");
    setSaveFeedback((items) => ({ ...items, [item.id]: { tone: "pending", message: "正在保存…" } }));
    try {
      const response = await fetch("/api/incentives/admin/submissions", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          id: next.id,
          kind: next.kind,
          nickname: next.nickname,
          email: next.email,
          title: next.title,
          body: next.body,
          status: next.status,
          reward_status: next.reward_status,
          developer_reply: next.developer_reply ?? "",
          is_flagged: next.is_flagged,
          is_public: next.is_public,
          like_count: next.like_count,
          created_at: next.created_at
        })
      });
      const result = await response.json().catch(() => ({})) as { error?: string; submission?: IncentiveSubmission };
      if (response.status === 401) {
        setAuth("login");
        throw new Error("登录已过期");
      }
      if (!response.ok || !result.submission) throw new Error(result.error ?? `保存失败（HTTP ${response.status}）`);
      editSubmission(item.id, result.submission, false);
      setSaveFeedback((items) => ({ ...items, [item.id]: { tone: "success", message: result.submission!.is_public ? "已保存并在前台展出" : "已保存" } }));
      return true;
    } catch (saveError) {
      const message = saveError instanceof Error ? saveError.message : "网络异常";
      setSaveFeedback((items) => ({ ...items, [item.id]: { tone: "error", message: `未保存：${message}` } }));
      setError(message);
      return false;
    } finally {
      setSavingId((current) => current === item.id ? "" : current);
    }
  }

  async function quickUpdate(item: IncentiveSubmission, patch: Partial<IncentiveSubmission>) {
    editSubmission(item.id, patch, false);
    if (!(await saveSubmission(item, patch))) editSubmission(item.id, item, false);
  }

  async function removeSubmission(item: IncentiveSubmission) {
    if (!window.confirm(`确定永久删除“${item.title}”吗？附件和点赞记录也会一并删除。`)) return;
    setSavingId(item.id);
    const response = await fetch("/api/incentives/admin/submissions", {
      method: "DELETE",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ id: item.id })
    });
    const result = await response.json().catch(() => ({})) as { error?: string };
    setSavingId("");
    if (!response.ok) {
      setError(result.error ?? "删除失败");
      return;
    }
    setSubmissions((items) => items.filter((current) => current.id !== item.id));
    if (editing?.id === item.id) setEditing(null);
  }

  const visibleSubmissions = useMemo(() => submissions.filter((item) =>
    (kindFilter === "all" || item.kind === kindFilter) &&
    (statusFilter === "all" || item.status === statusFilter)
  ).sort((left, right) => Number(right.is_flagged) - Number(left.is_flagged)), [submissions, kindFilter, statusFilter]);

  const pendingSubmissionCount = useMemo(
    () => submissions.filter((item) => item.status === "pending").length,
    [submissions]
  );

  const allVisibleSelected = visibleSubmissions.length > 0 &&
    visibleSubmissions.every((item) => selectedIds.includes(item.id));

  function toggleSelection(id: string) {
    setSelectedIds((ids) => ids.includes(id) ? ids.filter((value) => value !== id) : [...ids, id]);
  }

  function bulkPatch(action: BulkAction): Partial<IncentiveSubmission> {
    if (action.startsWith("status:")) {
      const status = action.slice(7) as SubmissionStatus;
      return { status, ...(status === "accepted" ? {} : { is_public: false }) };
    }
    if (action.startsWith("reward:")) return { reward_status: action.slice(7) as RewardStatus };
    if (action === "flag:on") return { is_flagged: true };
    if (action === "flag:off") return { is_flagged: false };
    if (action === "public:on") return { status: "accepted", is_public: true };
    if (action === "public:off") return { is_public: false };
    return {};
  }

  async function applyBulkAction(action: BulkAction) {
    const targets = submissions.filter((item) => selectedIds.includes(item.id));
    if (!targets.length) return;
    if (action === "delete" && !window.confirm(`确定永久删除选中的 ${targets.length} 条反馈吗？附件和点赞记录也会一并删除，此操作不可撤销。`)) return;
    const patch = action === "delete" ? {} : bulkPatch(action);
    setBulkSaving(true);
    setBulkMessage(action === "delete" ? `正在删除 ${targets.length} 条…` : `正在处理 ${targets.length} 条…`);
    try {
      const results = await Promise.all(targets.map(async (item) => {
        try {
          const response = await fetchWithTimeout("/api/incentives/admin/submissions", {
            method: action === "delete" ? "DELETE" : "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ id: item.id, ...(action === "delete" ? {} : patch) })
          });
          const result = await response.json().catch(() => ({})) as { error?: string; submission?: IncentiveSubmission };
          if (!response.ok || (action !== "delete" && !result.submission)) throw new Error(result.error ?? (action === "delete" ? "删除失败" : "更新失败"));
          return { id: item.id, success: true, submission: result.submission };
        } catch {
          return { id: item.id, success: false, submission: undefined };
        }
      }));
      const successfulIds = new Set(results.filter((result) => result.success).map((result) => result.id));
      if (action === "delete") {
        setSubmissions((items) => items.filter((item) => !successfulIds.has(item.id)));
        setSaveFeedback((items) => Object.fromEntries(Object.entries(items).filter(([id]) => !successfulIds.has(id))));
      } else {
        const updated = new Map(results.filter((result) => result.submission).map((result) => [result.id, result.submission!]));
        setSubmissions((items) => items.map((item) => updated.get(item.id) ?? item));
        setSaveFeedback((items) => ({
          ...items,
          ...Object.fromEntries(results.map((result) => [
            result.id,
            result.success
              ? { tone: "success", message: "批量更新成功" }
              : { tone: "error", message: "批量更新失败或请求超时" }
          ]))
        }));
      }
      const succeeded = successfulIds.size;
      const failed = results.length - succeeded;
      const verb = action === "delete" ? "删除" : "更新";
      setBulkMessage(failed ? `已${verb} ${succeeded} 条，${failed} 条失败或超时` : `已批量${verb} ${succeeded} 条`);
      setSelectedIds(results.filter((result) => !result.success).map((result) => result.id));
    } catch {
      setBulkMessage("批量操作失败，请重试");
    } finally {
      setBulkSaving(false);
    }
  }

  const eventOptions = useMemo(
    () => Array.from(new Set(accessLogs.map((item) => item.event_type)))
      .sort((left, right) => (eventLabels[left] ?? left).localeCompare(eventLabels[right] ?? right, "zh-CN")),
    [accessLogs]
  );

  const countryOptions = useMemo(() =>
    [...new Set(accessLogs.map((item) => item.country).filter((value): value is string => Boolean(value)))].sort(),
  [accessLogs]);
  const methodOptions = useMemo(() =>
    [...new Set(accessLogs.map((item) => item.method).filter(Boolean))].sort(),
  [accessLogs]);

  const visibleLogs = useMemo(() => {
    const query = logQuery.trim().toLocaleLowerCase();
    const from = logDateFrom ? new Date(logDateFrom).getTime() : Number.NEGATIVE_INFINITY;
    const to = logDateTo ? new Date(logDateTo).getTime() : Number.POSITIVE_INFINITY;
    return accessLogs.filter((item) => {
      const timestamp = Date.parse(item.created_at);
      const searchable = [
        item.ip_address,
        item.visitor_hash,
        item.country,
        item.region,
        item.city,
        item.path,
        item.event_type,
        item.method,
        item.referrer,
        item.user_agent,
        item.accept_language,
        item.request_id,
        item.forwarded_for,
        JSON.stringify(item.details)
      ].filter(Boolean).join(" ").toLocaleLowerCase();
      return (
        (scopeFilter === "all" || item.scope === scopeFilter) &&
        (severityFilter === "all" || item.severity === severityFilter) &&
        (eventFilter === "all" || item.event_type === eventFilter) &&
        (methodFilter === "all" || item.method === methodFilter) &&
        (countryFilter === "all" || item.country === countryFilter) &&
        (deviceFilter === "all" || deviceCategory(item.user_agent) === deviceFilter) &&
        timestamp >= from &&
        timestamp <= to &&
        (!query || searchable.includes(query))
      );
    });
  }, [
    accessLogs,
    countryFilter,
    deviceFilter,
    eventFilter,
    logDateFrom,
    logDateTo,
    logQuery,
    methodFilter,
    scopeFilter,
    severityFilter
  ]);

  function clearLogFilters() {
    setEventFilter("all");
    setScopeFilter("all");
    setSeverityFilter("all");
    setMethodFilter("all");
    setCountryFilter("all");
    setDeviceFilter("all");
    setLogQuery("");
    setLogDateFrom("");
    setLogDateTo("");
  }

  async function acknowledgeAlerts() {
    const response = await fetch("/api/incentives/admin/access-logs", { method: "PATCH" });
    if (!response.ok) {
      setError("提醒确认失败");
      return;
    }
    const now = new Date().toISOString();
    setAccessLogs((items) => items.map((item) => item.severity === "normal" ? item : { ...item, acknowledged_at: item.acknowledged_at ?? now }));
    setUnreadAlerts(0);
  }

  async function requestTranslation(entries: Array<{ key: string; text: string }>) {
    const response = await fetch("/api/incentives/admin/translate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ entries, targetLocales: translationLocales })
    });
    const result = await response.json().catch(() => ({})) as {
      error?: string;
      translations?: Record<string, Record<string, string>>;
    };
    if (response.status === 401) {
      setAuth("login");
      throw new Error("登录已过期");
    }
    if (!response.ok || translationLocales.some((locale) => !result.translations?.[locale])) {
      throw new Error(result.error ?? `翻译失败（HTTP ${response.status}）`);
    }
    return result.translations as LocalizedTranslations;
  }

  async function requestTranslations(entries: Array<{ key: string; text: string }>) {
    const batchSize = 12;
    const translations: LocalizedTranslations = { en: {}, "zh-tw": {}, ja: {} };
    for (let start = 0; start < entries.length; start += batchSize) {
      const translated = await requestTranslation(entries.slice(start, start + batchSize));
      for (const locale of translationLocales) {
        Object.assign(translations[locale], translated[locale]);
      }
    }
    return translations;
  }

  function nonEmptyLines(lines: string[]) {
    return lines.map((line) => line.trim()).filter(Boolean);
  }

  async function translateManagedFeatures() {
    if (translationRequestRef.current) {
      setFeatureMessage("另一项翻译仍在进行，请稍候");
      return;
    }
    const entries: Array<{ key: string; text: string }> = [];
    if (featureContent.summary.label_zh.trim()) entries.push({ key: "summary.label", text: featureContent.summary.label_zh });
    nonEmptyLines(featureContent.summary.items_zh).forEach((text, index) => entries.push({ key: `summary.items.${index}`, text }));
    featureContent.sections.filter((section) => section.release_version === activeFeatureVersion).forEach((section) => {
      const prefix = `section.${section.id}`;
      if (section.title_zh.trim()) entries.push({ key: `${prefix}.title`, text: section.title_zh });
      if (section.body_zh.trim()) entries.push({ key: `${prefix}.body`, text: section.body_zh });
      nonEmptyLines(section.items_zh).forEach((text, index) => entries.push({ key: `${prefix}.items.${index}`, text }));
    });
    if (!entries.length) {
      setFeatureMessage("请先填写需要翻译的中文内容");
      return;
    }
    translationRequestRef.current = "features";
    setTranslationSaving("features");
    setError("");
    try {
      const translations = await requestTranslations(entries);
      setFeatureContent((content) => ({
        ...content,
        summary: {
          ...content.summary,
          label_en: translations.en["summary.label"] ?? content.summary.label_en,
          label_zh_tw: translations["zh-tw"]["summary.label"] ?? content.summary.label_zh_tw,
          label_ja: translations.ja["summary.label"] ?? content.summary.label_ja,
          items_en: nonEmptyLines(content.summary.items_zh).map((_, index) => translations.en[`summary.items.${index}`] ?? content.summary.items_en[index] ?? ""),
          items_zh_tw: nonEmptyLines(content.summary.items_zh).map((_, index) => translations["zh-tw"][`summary.items.${index}`] ?? content.summary.items_zh_tw[index] ?? ""),
          items_ja: nonEmptyLines(content.summary.items_zh).map((_, index) => translations.ja[`summary.items.${index}`] ?? content.summary.items_ja[index] ?? "")
        },
        sections: content.sections.map((section) => {
          if (section.release_version !== activeFeatureVersion) return section;
          const prefix = `section.${section.id}`;
          return {
            ...section,
            title_en: translations.en[`${prefix}.title`] ?? section.title_en,
            title_zh_tw: translations["zh-tw"][`${prefix}.title`] ?? section.title_zh_tw,
            title_ja: translations.ja[`${prefix}.title`] ?? section.title_ja,
            body_en: translations.en[`${prefix}.body`] ?? section.body_en,
            body_zh_tw: translations["zh-tw"][`${prefix}.body`] ?? section.body_zh_tw,
            body_ja: translations.ja[`${prefix}.body`] ?? section.body_ja,
            items_en: nonEmptyLines(section.items_zh).map((_, index) => translations.en[`${prefix}.items.${index}`] ?? section.items_en[index] ?? ""),
            items_zh_tw: nonEmptyLines(section.items_zh).map((_, index) => translations["zh-tw"][`${prefix}.items.${index}`] ?? section.items_zh_tw[index] ?? ""),
            items_ja: nonEmptyLines(section.items_zh).map((_, index) => translations.ja[`${prefix}.items.${index}`] ?? section.items_ja[index] ?? "")
          };
        })
      }));
      setFeatureMessage("英文、繁中和日文内容已自动生成，请确认后保存并同步前台");
    } catch (translationError) {
      const message = translationError instanceof Error ? translationError.message : "翻译失败";
      setFeatureMessage(`自动翻译未完成：${message}`);
      setError(message);
    } finally {
      if (translationRequestRef.current === "features") {
        translationRequestRef.current = null;
        setTranslationSaving(null);
      }
    }
  }

  function parsePreviewBulkInput() {
    if (previewTranslationLocked) return;
    const items = splitPreviewLines(previewBulkInput);
    if (!items.length) {
      setError("请先粘贴至少一条中文功能内容");
      return;
    }
    setPreviewDraft((draft) => {
      const usedIds = new Set<string>();
      const matched = items.map((contentZh) => {
        const existing = draft.features.find((feature) => !usedIds.has(feature.id) && feature.content_zh.trim() === contentZh);
        if (existing) usedIds.add(existing.id);
        return existing ?? null;
      });
      const exactMatchMoved = matched.some((feature, index) => feature && draft.features[index]?.id !== feature.id);
      const unmatchedItemIndexes = matched
        .map((feature, index) => feature ? -1 : index)
        .filter((index) => index >= 0);
      const unmatchedExisting = draft.features.filter((feature) => !usedIds.has(feature.id));
      const uniqueEditedPair = unmatchedItemIndexes.length === 1 && unmatchedExisting.length === 1
        ? { itemIndex: unmatchedItemIndexes[0], feature: unmatchedExisting[0] }
        : null;
      const features = items.map((contentZh, index) => {
        const exact = matched[index];
        if (exact) return { ...exact, content_zh: contentZh, sort_order: index + 1 };
        const positional = !exactMatchMoved ? draft.features[index] : null;
        const inherited = uniqueEditedPair?.itemIndex === index
          ? uniqueEditedPair.feature
          : positional && !usedIds.has(positional.id)
            ? positional
            : null;
        if (inherited && !usedIds.has(inherited.id)) {
          usedIds.add(inherited.id);
          return {
            ...inherited,
            content_zh: contentZh,
            content_en: "",
            content_zh_tw: "",
            content_ja: "",
            sort_order: index + 1
          };
        }
        return { ...newPreviewFeature(contentZh), sort_order: index + 1 };
      });
      return { ...draft, features };
    });
    setError("");
  }

  function updatePreviewFeature(id: string, patch: Partial<ReleasePreviewFeature>) {
    if (previewTranslationLocked) return;
    setPreviewDraft((draft) => ({
      ...draft,
      features: draft.features.map((feature) => feature.id === id ? { ...feature, ...patch } : feature)
    }));
  }

  function movePreviewFeature(id: string, direction: -1 | 1) {
    if (previewTranslationLocked) return;
    setPreviewDraft((draft) => {
      const index = draft.features.findIndex((feature) => feature.id === id);
      const target = index + direction;
      if (index < 0 || target < 0 || target >= draft.features.length) return draft;
      const features = [...draft.features];
      [features[index], features[target]] = [features[target], features[index]];
      return { ...draft, features: features.map((feature, order) => ({ ...feature, sort_order: order + 1 })) };
    });
  }

  function removePreviewFeature(id: string) {
    if (previewTranslationLocked) return;
    setPreviewDraft((draft) => ({
      ...draft,
      features: draft.features
        .filter((feature) => feature.id !== id)
        .map((feature, index) => ({ ...feature, sort_order: index + 1 }))
    }));
  }

  function addPreviewFeature() {
    if (previewTranslationLocked) return;
    setPreviewDraft((draft) => ({
      ...draft,
      features: [...draft.features, { ...newPreviewFeature(), sort_order: draft.features.length + 1 }]
    }));
  }

  async function translatePreview(mode: "all" | "missing" = "all") {
    if (translationRequestRef.current) {
      setError("另一项翻译仍在进行，请稍候");
      return;
    }
    const sourceSignature = previewTranslationSignature(previewDraft);
    const entries: Array<{ key: string; text: string }> = [];
    if (previewDraft.note_zh.trim()) entries.push({ key: "note", text: previewDraft.note_zh });
    previewDraft.features.forEach((feature) => {
      if (feature.content_zh.trim()) entries.push({ key: `feature.${feature.id}`, text: feature.content_zh });
    });
    if (!entries.length) {
      setError("请先填写中文版本说明或功能内容");
      return;
    }
    translationRequestRef.current = "preview";
    setTranslationSaving("preview");
    setError("");
    try {
      const translations = await requestTranslations(entries);
      if (previewTranslationSignature(previewDraftRef.current) !== sourceSignature) {
        setError("预告中文内容已变化，已丢弃过期翻译结果，请重新翻译");
        return;
      }
      setPreviewDraft((draft) => ({
        ...draft,
        note_en: mode === "missing" && draft.note_en.trim() ? draft.note_en : (translations.en.note ?? draft.note_en),
        note_zh_tw: mode === "missing" && draft.note_zh_tw.trim() ? draft.note_zh_tw : (translations["zh-tw"].note ?? draft.note_zh_tw),
        note_ja: mode === "missing" && draft.note_ja.trim() ? draft.note_ja : (translations.ja.note ?? draft.note_ja),
        features: draft.features.map((feature) => {
          const key = `feature.${feature.id}`;
          return {
            ...feature,
            content_en: mode === "missing" && feature.content_en.trim() ? feature.content_en : (translations.en[key] ?? feature.content_en),
            content_zh_tw: mode === "missing" && feature.content_zh_tw.trim() ? feature.content_zh_tw : (translations["zh-tw"][key] ?? feature.content_zh_tw),
            content_ja: mode === "missing" && feature.content_ja.trim() ? feature.content_ja : (translations.ja[key] ?? feature.content_ja)
          };
        })
      }));
    } catch (translationError) {
      setError(translationError instanceof Error ? translationError.message : "翻译失败");
    } finally {
      if (translationRequestRef.current === "preview") {
        translationRequestRef.current = null;
        setTranslationSaving(null);
      }
    }
  }

  async function savePreview(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (previewTranslationLocked) return;
    setError("");
    const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
    const status = submitter?.value === "published" ? "published" : previewDraft.status;
    setPreviewSaving(true);
    try {
      const response = await fetch("/api/incentives/admin/previews", {
        method: previewDraft.id ? "PATCH" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...(previewDraft.id ? { id: previewDraft.id } : {}),
          version: previewDraft.version,
          note_zh: previewDraft.note_zh,
          note_en: previewDraft.note_en,
          note_zh_tw: previewDraft.note_zh_tw,
          note_ja: previewDraft.note_ja,
          features: previewDraft.features,
          target_date: targetDateForReleaseTiming(previewTimingPreset, previewCustomDays) ?? "",
          status
        })
      });
      const result = (await response.json()) as { error?: string; preview?: ReleasePreview };
      if (!response.ok || !result.preview) {
        setError(result.error ?? "保存失败");
        return;
      }
      const savedPreview = result.preview;
      setPreviews((items) => savedPreview.status === "published"
        ? (items.some((item) => item.id === savedPreview.id)
          ? items.map((item) => item.id === savedPreview.id ? savedPreview : item)
          : [savedPreview, ...items])
        : items.filter((item) => item.id !== savedPreview.id));
      setDraftPreviews((items) => savedPreview.status === "draft"
        ? (items.some((item) => item.id === savedPreview.id)
          ? items.map((item) => item.id === savedPreview.id ? savedPreview : item)
          : [savedPreview, ...items])
        : items.filter((item) => item.id !== savedPreview.id));
      const draft = previewToDraft(result.preview);
      setPreviewDraft(draft);
      setPreviewBulkInput(draft.features.map((feature) => feature.content_zh).join("\n"));
      const timing = releaseTimingFromTargetDate(result.preview.target_date);
      setPreviewTimingPreset(timing.preset);
      setPreviewCustomDays(timing.days ?? 14);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "保存失败");
    } finally {
      setPreviewSaving(false);
    }
  }

  function editPreview(preview: ReleasePreview) {
    if (previewTranslationLocked) return;
    const draft = previewToDraft(preview);
    setPreviewDraft(draft);
    setPreviewBulkInput(draft.features.map((feature) => feature.content_zh).join("\n"));
    setPreviewLocale("zh");
    const timing = releaseTimingFromTargetDate(preview.target_date);
    setPreviewTimingPreset(timing.preset);
    setPreviewCustomDays(timing.days ?? 14);
    setDraftMenuOpen(false);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function newPreview() {
    if (previewTranslationLocked) return;
    setPreviewDraft(emptyPreviewDraft);
    setPreviewBulkInput("");
    setPreviewLocale("zh");
    setPreviewTimingPreset("tbd");
    setPreviewCustomDays(14);
    setDraftMenuOpen(false);
  }

  async function togglePreview(preview: ReleasePreview) {
    const status = preview.status === "published" ? "draft" : "published";
    const response = await fetch("/api/incentives/admin/previews", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ id: preview.id, status })
    });
    const result = (await response.json()) as { error?: string; preview?: ReleasePreview };
    if (!response.ok || !result.preview) {
      setError(result.error ?? "更新失败");
      return;
    }
    const updatedPreview = result.preview;
    setPreviews((items) => updatedPreview.status === "published"
      ? (items.some((item) => item.id === updatedPreview.id)
        ? items.map((item) => item.id === updatedPreview.id ? updatedPreview : item)
        : [updatedPreview, ...items])
      : items.filter((item) => item.id !== updatedPreview.id));
    setDraftPreviews((items) => updatedPreview.status === "draft"
      ? (items.some((item) => item.id === updatedPreview.id)
        ? items.map((item) => item.id === updatedPreview.id ? updatedPreview : item)
        : [updatedPreview, ...items])
      : items.filter((item) => item.id !== updatedPreview.id));
    if (previewDraft.id === preview.id) {
      setPreviewDraft((draft) => ({ ...draft, status: result.preview!.status }));
    }
  }

  function markFeatureDirty() {
    setFeatureMessage("有未保存的更改");
  }

  function updateFeatureSummary(patch: Partial<FeatureContent["summary"]>) {
    setFeatureContent((content) => ({
      ...content,
      summary: { ...content.summary, ...patch }
    }));
    markFeatureDirty();
  }

  function updateFeatureSection(id: string, patch: Partial<FeatureContentSection>) {
    setFeatureContent((content) => ({
      ...content,
      sections: content.sections.map((section) => section.id === id ? { ...section, ...patch } : section)
    }));
    markFeatureDirty();
  }

  function moveFeatureSection(id: string, direction: -1 | 1) {
    setFeatureContent((content) => {
      const index = content.sections.findIndex((section) => section.id === id);
      const groupIndexes = content.sections
        .map((section, sectionIndex) => section.release_version === activeFeatureVersion ? sectionIndex : -1)
        .filter((sectionIndex) => sectionIndex >= 0);
      const groupPosition = groupIndexes.indexOf(index);
      const target = groupIndexes[groupPosition + direction];
      if (index < 0 || groupPosition < 0 || target === undefined) return content;
      const sections = [...content.sections];
      [sections[index], sections[target]] = [sections[target], sections[index]];
      return { ...content, sections };
    });
    markFeatureDirty();
  }

  function addFeatureSection() {
    if (activeFeatureVersion === LEGACY_FEATURE_RELEASE_VERSION || !isFeatureReleaseVersion(activeFeatureVersion)) {
      setFeatureMessage("请先在版本预告中创建规范的完整版本号（例如 v2.1.8），再回来新增功能条目");
      return;
    }
    const id = typeof crypto !== "undefined" && "randomUUID" in crypto
      ? crypto.randomUUID()
      : `feature-${Date.now()}`;
    setFeatureContent((content) => ({
      ...content,
      sections: [...content.sections, {
        id,
        release_version: activeFeatureVersion,
        major_version: "OTHER",
        title_zh: "",
        title_en: "",
        title_zh_tw: "",
        title_ja: "",
        body_zh: "",
        body_en: "",
        body_zh_tw: "",
        body_ja: "",
        items_zh: [],
        items_en: [],
        items_zh_tw: [],
        items_ja: [],
        visible: false
      }]
    }));
    markFeatureDirty();
  }

  function deleteFeatureSection(section: FeatureContentSection) {
    if (!window.confirm(`确定删除“${section.title_zh || section.title_en || "未命名条目"}”吗？保存后前台也会删除。`)) return;
    setFeatureContent((content) => ({
      ...content,
      sections: content.sections.filter((item) => item.id !== section.id)
    }));
    markFeatureDirty();
  }

  async function saveManagedFeatures() {
    const incompleteVersionSection = featureContent.sections.find((section) =>
      !isFeatureReleaseVersion(section.release_version)
    );
    if (incompleteVersionSection) {
      setFeatureMessage(`未保存：“${incompleteVersionSection.title_zh || incompleteVersionSection.title_en || "未命名条目"}”必须填写完整版本号（例如 v2.1.8）`);
      return;
    }
    const incompleteVisibleSection = featureContent.sections.find((section) =>
      section.visible &&
      (!section.title_zh.trim() || !section.title_en.trim() || !section.body_zh.trim() || !section.body_en.trim())
    );
    if (incompleteVisibleSection) {
      setFeatureMessage(`未保存：“${incompleteVisibleSection.title_zh || incompleteVisibleSection.title_en || "未命名条目"}”显示前需补全中英文标题和描述`);
      return;
    }
    setFeatureSaving(true);
    setFeatureMessage("正在保存并同步前台…");
    setError("");
    try {
      const response = await fetch("/api/incentives/admin/features", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ content: featureContent })
      });
      const result = await response.json().catch(() => ({})) as { error?: string; content?: unknown };
      if (response.status === 401) {
        setAuth("login");
        throw new Error("登录已过期");
      }
      if (!response.ok || !result.content) throw new Error(result.error ?? "保存失败");
      setFeatureContent(sanitizeFeatureContent(result.content));
      setFeatureMessage("已保存，官网新功能页已同步");
    } catch (saveError) {
      const message = saveError instanceof Error ? saveError.message : "网络异常";
      setFeatureMessage(`未保存：${message}`);
      setError(message);
    } finally {
      setFeatureSaving(false);
    }
  }

  if (auth === "checking") return <main className="adminLoading">正在确认后台身份…</main>;

  if (auth === "login") {
    return (
      <main className="adminLoginPage">
        <section className="adminLoginCard">
          <LogoLockup />
          <p className="eyebrow"><span aria-hidden="true">•</span>维护者后台</p>
          <h1>审阅用户提交</h1>
          <p>登录后可以管理反馈、新功能页、版本预告与访问日志。</p>
          <form onSubmit={login}>
            <label><span>后台密码</span><input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" required /></label>
            <button className="button buttonPrimary" type="submit">进入后台</button>
          </form>
          {error && <p className="adminError" role="alert">{error}</p>}
          <Link href="/incentives">← 返回用户激励计划</Link>
        </section>
      </main>
    );
  }

  return (
    <div className="adminShell">
      <AdminNav
        active={panel === "promo-codes" ? "promo-codes" : "feedback"}
        activePanel={panel}
        pendingCount={pendingSubmissionCount}
        unreadAlerts={unreadAlerts}
        onNavigate={(navPanel) => setPanel(navPanel as Panel)}
        onLogout={logout}
      />

      <main className="adminMain">
        {unreadAlerts > 0 && (
          <section className="securityAlert" role="alert">
            <div><strong>发现 {unreadAlerts} 条未确认的异常后台访问</strong><p>包括登录失败、跨站请求或未授权的敏感操作。请在访问日志中核查。</p></div>
            <div><button className="button buttonSecondary" onClick={() => setPanel("access")}>查看详情</button><button className="button buttonPrimary" onClick={acknowledgeAlerts}>标记已读</button></div>
          </section>
        )}
        {error && <p className="adminError" role="alert">{error}</p>}

        {panel === "submissions" && (
          <>
            <header className="adminPageHeader">
              <div><p>FEEDBACK DATABASE</p><h2>反馈管理</h2></div>
              <div className="adminToolbar">
                <div className="adminViewToggle" aria-label="视图切换"><button className={viewMode === "table" ? "isActive" : ""} onClick={() => setViewMode("table")}>表格</button><button className={viewMode === "cards" ? "isActive" : ""} onClick={() => setViewMode("cards")}>卡片</button></div>
                <div className="adminFilters">
                  <div className="adminFilterGroup" aria-label="提交类型">{([["all", "全部"], ["feature", "新功能"], ["bug", "Bug"]] as const).map(([value, label]) => <button type="button" className={kindFilter === value ? "isActive" : ""} aria-pressed={kindFilter === value} onClick={() => setKindFilter(value)} key={value}>{label}</button>)}</div>
                  <div className="adminFilterGroup" aria-label="审阅状态"><button type="button" className={statusFilter === "all" ? "isActive" : ""} aria-pressed={statusFilter === "all"} onClick={() => setStatusFilter("all")}>全部</button>{Object.entries(compactStatusLabels).map(([value, label]) => <button type="button" className={statusFilter === value ? "isActive" : ""} aria-pressed={statusFilter === value} onClick={() => setStatusFilter(value as SubmissionStatus)} key={value}>{label}</button>)}</div>
                </div>
              </div>
            </header>

            <section className="bulkActionBar" aria-label="批量操作">
              <label className="bulkSelectAll">
                <input
                  type="checkbox"
                  checked={allVisibleSelected}
                  onChange={() => setSelectedIds((ids) => allVisibleSelected
                    ? ids.filter((id) => !visibleSubmissions.some((item) => item.id === id))
                    : [...new Set([...ids, ...visibleSubmissions.map((item) => item.id)])])}
                />
                <span>已选 <strong>{selectedIds.length}</strong> / 当前 {visibleSubmissions.length}</span>
              </label>
              <div className="bulkActionGroup"><span>审阅</span>{Object.entries(compactStatusLabels).map(([value, label]) => <button type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction(`status:${value as SubmissionStatus}`)} key={value}>{label}</button>)}</div>
              <div className="bulkActionGroup"><span>奖励</span>{Object.entries(compactRewardLabels).map(([value, label]) => <button type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction(`reward:${value as RewardStatus}`)} key={value}>{label}</button>)}</div>
              <div className="bulkActionGroup"><span>管理</span><button type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction("flag:on")}>加红旗</button><button type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction("flag:off")}>去红旗</button><button type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction("public:on")}>展出</button><button type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction("public:off")}>撤下</button><button className="danger" type="button" disabled={!selectedIds.length || bulkSaving} onClick={() => void applyBulkAction("delete")}>删除</button></div>
              {selectedIds.length > 0 && <button className="bulkClearButton" type="button" disabled={bulkSaving} onClick={() => setSelectedIds([])}>清空选择</button>}
              {bulkMessage && <span className="bulkMessage" role="status">{bulkMessage}</span>}
            </section>

            {viewMode === "table" ? (
              <div className="adminTableWrap">
                <table className="adminDataTable feedbackTable">
                  <colgroup><col className="feedbackColumn" /><col className="submitterColumn" /><col className="workflowColumn" /><col className="metricsColumn" /><col className="actionsColumn" /></colgroup>
                  <thead><tr><th>反馈</th><th>提交者</th><th>流程</th><th>数据</th><th>操作</th></tr></thead>
                  <tbody>{visibleSubmissions.map((item) => (
                    <tr className={item.is_flagged ? "isFlagged" : ""} key={item.id}>
                      <td><div className="tableTitle"><input type="checkbox" checked={selectedIds.includes(item.id)} onChange={() => toggleSelection(item.id)} aria-label={`选择 ${item.title}`} /><span className={`kindBadge ${item.kind}`}>{item.kind === "feature" ? "新功能" : "Bug"}</span><strong>{item.title}</strong></div><p>{item.body}</p>{saveFeedback[item.id] && <small className={saveFeedback[item.id].tone}>{saveFeedback[item.id].message}</small>}</td>
                      <td className="tableSubmitter"><strong>@{item.nickname}</strong><a href={`mailto:${item.email}`} title={item.email}>{item.email}</a></td>
                      <td><div className="tableWorkflow"><div className="tableButtonGroup statusButtons" aria-label="审阅状态">{Object.entries(compactStatusLabels).map(([value, label]) => <button type="button" title={statusLabels[value as SubmissionStatus]} className={item.status === value ? "isActive" : ""} disabled={savingId === item.id} onClick={() => void quickUpdate(item, { status: value as SubmissionStatus, ...(value === "accepted" ? {} : { is_public: false }) })} key={value}>{label}</button>)}</div><div className="tableButtonGroup rewardButtons" aria-label="奖励状态">{Object.entries(compactRewardLabels).map(([value, label]) => <button type="button" title={rewardLabels[value as RewardStatus]} className={item.reward_status === value ? "isActive" : ""} disabled={savingId === item.id} onClick={() => void quickUpdate(item, { reward_status: value as RewardStatus })} key={value}>{label}</button>)}</div><button className={`displayToggle ${item.is_public ? "isPublic" : ""}`} disabled={savingId === item.id} onClick={() => void quickUpdate(item, item.is_public ? { is_public: false } : { status: "accepted", is_public: true })} aria-pressed={item.is_public}><span aria-hidden="true" />{item.is_public ? "前台展出" : "前台隐藏"}</button></div></td>
                      <td><div className="tableMetrics"><strong>♥ {item.like_count}</strong><time title={formatDate(item.created_at)}>提交 {formatCompactDate(item.created_at)}</time><time title={formatDate(item.updated_at)}>更新 {formatCompactDate(item.updated_at)}</time></div></td>
                      <td><div className="tableActions"><button onClick={() => setEditing({ ...item })}>编辑</button><button className="danger" disabled={savingId === item.id} onClick={() => void removeSubmission(item)}>删除</button></div></td>
                    </tr>
                  ))}</tbody>
                </table>
                {!visibleSubmissions.length && <p className="adminEmpty">当前筛选条件下没有提交。</p>}
              </div>
            ) : (
              <div className="submissionQueue">
                {visibleSubmissions.map((item) => (
                  <article className={`reviewCard ${item.is_flagged ? "isFlagged" : ""}`} key={item.id}>
                    <header><div><input type="checkbox" checked={selectedIds.includes(item.id)} onChange={() => toggleSelection(item.id)} aria-label={`选择 ${item.title}`} /><span className={`kindBadge ${item.kind}`}>{item.kind === "feature" ? "新功能" : "Bug"}</span><time>{formatDate(item.created_at)}</time><span>♥ {item.like_count}</span></div><strong>{item.title}</strong></header>
                    <p className="reviewBody">{item.body}</p>
                    <div className="reviewIdentity"><span>@{item.nickname}</span><a href={`mailto:${item.email}`}>{item.email}</a></div>
                    {item.attachments.length > 0 && <div className="reviewAttachments">{item.attachments.map((attachment) => attachment.signedUrl ? <a href={attachment.signedUrl} target="_blank" rel="noreferrer" key={attachment.path}>{attachment.type.startsWith("video/") ? "视频" : "图片"} · {attachment.name} <ExternalArrow /></a> : <span key={attachment.path}>{attachment.name}</span>)}</div>}
                    <div className="reviewControls">
                      <div className="reviewOption"><span>审阅状态</span><div className="reviewButtonGroup">{Object.entries(statusLabels).map(([value, label]) => <button type="button" className={item.status === value ? "isActive" : ""} aria-pressed={item.status === value} onClick={() => editSubmission(item.id, { status: value as SubmissionStatus, ...(value === "accepted" ? {} : { is_public: false }) })} key={value}>{label}</button>)}</div></div>
                      <div className="reviewOption"><span>奖励</span><div className="reviewButtonGroup">{Object.entries(rewardLabels).map(([value, label]) => <button type="button" className={item.reward_status === value ? "isActive" : ""} aria-pressed={item.reward_status === value} onClick={() => editSubmission(item.id, { reward_status: value as RewardStatus })} key={value}>{label}</button>)}</div></div>
                      <div className="reviewOption"><span>管理</span><div className="reviewButtonGroup"><button type="button" className={item.is_flagged ? "isFlagged" : ""} onClick={() => editSubmission(item.id, { is_flagged: !item.is_flagged })}>🚩 {item.is_flagged ? "已标注" : "红旗标注"}</button><button type="button" className={item.is_public ? "isPublic" : ""} onClick={() => editSubmission(item.id, item.is_public ? { is_public: false } : { status: "accepted", is_public: true })}>{item.is_public ? "✓ 正在展出" : "一键展出"}</button><button type="button" onClick={() => setEditing({ ...item })}>编辑内容</button><button type="button" className="danger" onClick={() => void removeSubmission(item)}>删除</button></div></div>
                      <label className="reviewNote"><span>开发者回复</span><textarea value={item.developer_reply ?? ""} onChange={(event) => editSubmission(item.id, { developer_reply: event.target.value })} rows={2} /></label>
                      <div className="reviewSaveRow"><button className="button buttonPrimary" type="button" onClick={() => void saveSubmission(item)} disabled={savingId === item.id}>{savingId === item.id ? "保存中…" : "保存审阅"}</button><span className={`reviewSaveFeedback ${saveFeedback[item.id]?.tone ?? ""}`} role="status">{saveFeedback[item.id]?.message ?? "修改后请保存"}</span></div>
                    </div>
                  </article>
                ))}
                {!visibleSubmissions.length && <p className="adminEmpty">当前筛选条件下没有提交。</p>}
              </div>
            )}
          </>
        )}

        {panel === "features" && (
          <>
            <header className="adminPageHeader">
              <div><p>FEATURE PAGE CONTENT</p><h2>新功能页内容</h2></div>
              <div className="featureAdminActions">
                <span role="status">{featureMessage || "现有四种语言内容已导入，可直接修改"}</span>
                <button className="button buttonSecondary" type="button" disabled={featureSaving || translationLocked} onClick={() => void translateManagedFeatures()}>{translationSaving === "features" ? "正在翻译…" : "从中文自动翻译其他语言"}</button>
                <label><span>版本号</span><select value={activeFeatureVersion} onChange={(event) => setSelectedFeatureVersion(event.target.value)}>{featureVersionOptions.map((version) => <option value={version} key={version}>{version}</option>)}</select></label>
                <button className="button buttonSecondary" type="button" onClick={addFeatureSection}>新增功能条目</button>
                <button className="button buttonPrimary" type="button" disabled={featureSaving || translationLocked} onClick={() => void saveManagedFeatures()}>{featureSaving ? "保存中…" : "保存并同步前台"}</button>
              </div>
            </header>

            <section className="featureAdminSummary">
              <header>
                <div><strong>本次重点</strong><small>管理顶部重点摘要，四种语言同步保存</small></div>
                <button className={`displayToggle ${featureContent.summary.visible ? "isPublic" : ""}`} type="button" onClick={() => updateFeatureSummary({ visible: !featureContent.summary.visible })}><span aria-hidden="true" />{featureContent.summary.visible ? "前台显示" : "前台隐藏"}</button>
              </header>
              <div className="featureLanguageGrid">
                <div>
                  <label><span>中文标题</span><input value={featureContent.summary.label_zh} onChange={(event) => updateFeatureSummary({ label_zh: event.target.value })} /></label>
                  <label><span>中文分条（每行一条）</span><textarea rows={6} value={featureContent.summary.items_zh.join("\n")} onChange={(event) => updateFeatureSummary({ items_zh: event.target.value.split("\n") })} /></label>
                </div>
                <div>
                  <label><span>English title</span><input value={featureContent.summary.label_en} onChange={(event) => updateFeatureSummary({ label_en: event.target.value })} /></label>
                  <label><span>English bullets (one per line)</span><textarea rows={6} value={featureContent.summary.items_en.join("\n")} onChange={(event) => updateFeatureSummary({ items_en: event.target.value.split("\n") })} /></label>
                </div>
                <div>
                  <label><span>繁中標題</span><input value={featureContent.summary.label_zh_tw} onChange={(event) => updateFeatureSummary({ label_zh_tw: event.target.value })} /></label>
                  <label><span>繁中分點（每行一條）</span><textarea rows={6} value={featureContent.summary.items_zh_tw.join("\n")} onChange={(event) => updateFeatureSummary({ items_zh_tw: event.target.value.split("\n") })} /></label>
                </div>
                <div>
                  <label><span>日本語の見出し</span><input value={featureContent.summary.label_ja} onChange={(event) => updateFeatureSummary({ label_ja: event.target.value })} /></label>
                  <label><span>日本語の箇条書き（1行ずつ）</span><textarea rows={6} value={featureContent.summary.items_ja.join("\n")} onChange={(event) => updateFeatureSummary({ items_ja: event.target.value.split("\n") })} /></label>
                </div>
              </div>
            </section>

            <div className="featureAdminList">
              {activeFeatureSections.map((section, index) => (
                <article key={section.id}>
                  <header>
                    <div><span>{String(index + 1).padStart(2, "0")}</span><strong>{section.title_zh || section.title_en || "未命名功能条目"}</strong></div>
                    <div className="featureRowActions">
                      <button type="button" disabled={index === 0} onClick={() => moveFeatureSection(section.id, -1)}>上移</button>
                      <button type="button" disabled={index === activeFeatureSections.length - 1} onClick={() => moveFeatureSection(section.id, 1)}>下移</button>
                      <button className={section.visible ? "isVisible" : ""} type="button" onClick={() => updateFeatureSection(section.id, { visible: !section.visible })}>{section.visible ? "正在显示" : "已隐藏"}</button>
                      <button className="danger" type="button" onClick={() => deleteFeatureSection(section)}>删除</button>
                    </div>
                  </header>
                  <div className="featureLanguageGrid">
                    <div>
                      <label><span>中文标题</span><input value={section.title_zh} onChange={(event) => updateFeatureSection(section.id, { title_zh: event.target.value })} /></label>
                      <label><span>中文描述</span><textarea rows={4} value={section.body_zh} onChange={(event) => updateFeatureSection(section.id, { body_zh: event.target.value })} /></label>
                      <label><span>中文分条（每行一条）</span><textarea rows={7} value={section.items_zh.join("\n")} onChange={(event) => updateFeatureSection(section.id, { items_zh: event.target.value.split("\n") })} /></label>
                    </div>
                    <div>
                      <label><span>English title</span><input value={section.title_en} onChange={(event) => updateFeatureSection(section.id, { title_en: event.target.value })} /></label>
                      <label><span>English description</span><textarea rows={4} value={section.body_en} onChange={(event) => updateFeatureSection(section.id, { body_en: event.target.value })} /></label>
                      <label><span>English bullets (one per line)</span><textarea rows={7} value={section.items_en.join("\n")} onChange={(event) => updateFeatureSection(section.id, { items_en: event.target.value.split("\n") })} /></label>
                    </div>
                    <div>
                      <label><span>繁中標題</span><input value={section.title_zh_tw} onChange={(event) => updateFeatureSection(section.id, { title_zh_tw: event.target.value })} /></label>
                      <label><span>繁中描述</span><textarea rows={4} value={section.body_zh_tw} onChange={(event) => updateFeatureSection(section.id, { body_zh_tw: event.target.value })} /></label>
                      <label><span>繁中分點（每行一條）</span><textarea rows={7} value={section.items_zh_tw.join("\n")} onChange={(event) => updateFeatureSection(section.id, { items_zh_tw: event.target.value.split("\n") })} /></label>
                    </div>
                    <div>
                      <label><span>日本語の見出し</span><input value={section.title_ja} onChange={(event) => updateFeatureSection(section.id, { title_ja: event.target.value })} /></label>
                      <label><span>日本語の説明</span><textarea rows={4} value={section.body_ja} onChange={(event) => updateFeatureSection(section.id, { body_ja: event.target.value })} /></label>
                      <label><span>日本語の箇条書き（1行ずつ）</span><textarea rows={7} value={section.items_ja.join("\n")} onChange={(event) => updateFeatureSection(section.id, { items_ja: event.target.value.split("\n") })} /></label>
                    </div>
                  </div>
                </article>
              ))}
              {!activeFeatureSections.length && <p className="adminEmpty">该版本还没有功能条目，请先选择其他版本或新增一条。</p>}
            </div>
          </>
        )}

        {panel === "previews" && (
          <>
            <header className="adminPageHeader"><div><p>RELEASE PREVIEW</p><h2>发布版本预告</h2></div><div className="featureAdminActions">{draftPreviews.length > 0 && <div className="previewDraftMenu"><button className="button buttonSecondary" type="button" aria-expanded={draftMenuOpen} disabled={previewTranslationLocked} onClick={() => setDraftMenuOpen((open) => !open)}>草稿箱（{draftPreviews.length}）</button>{draftMenuOpen && <div className="previewDraftMenuPanel">{draftPreviews.map((preview) => <button type="button" disabled={previewTranslationLocked} onClick={() => editPreview(preview)} key={preview.id}>{preview.version} · {formatReleaseTiming(preview.target_date, "zh")}</button>)}</div>}</div>}<button className="button buttonSecondary" type="button" disabled={previewTranslationLocked} onClick={newPreview}>新建预告</button></div></header>
            <form className="previewEditor" onSubmit={savePreview}>
              <div className="previewEditorMeta">
                <label><span>版本号</span><input name="version" placeholder="例如：v3.2" value={previewDraft.version} disabled={previewTranslationLocked} onChange={(event) => setPreviewDraft((draft) => ({ ...draft, version: event.target.value }))} required /></label>
                <label><span>预计上线范围</span><select name="target_timing" value={previewTimingPreset} disabled={previewTranslationLocked} onChange={(event) => setPreviewTimingPreset(event.target.value as ReleaseTimingPreset)}>{releaseTimingOptions.map((option) => <option value={option.value} key={option.value}>{option.label}</option>)}</select></label>
                {previewTimingPreset === "custom-days" && <label><span>天数（自动递减）</span><input name="target_days" type="number" min={2} max={365} step={1} value={previewCustomDays} disabled={previewTranslationLocked} onChange={(event) => setPreviewCustomDays(Number(event.target.value))} required /></label>}
              </div>

              <div className="featureLocaleTabs previewLocaleTabs" role="tablist" aria-label="版本预告语言">
                {previewLocaleOptions.map((option) => <button type="button" role="tab" aria-selected={previewLocale === option.value} className={previewLocale === option.value ? "isActive" : ""} disabled={previewTranslationLocked} onClick={() => setPreviewLocale(option.value)} key={option.value}>{option.label}</button>)}
              </div>

              <div className="previewEditorLanguages">
                <label>
                  <span>{previewLocale === "zh" ? "版本说明 Note（中文）" : previewLocale === "en" ? "Version Note (English)" : previewLocale === "zh-tw" ? "版本說明 Note（繁中）" : "バージョンノート（日本語）"}</span>
                  <textarea
                    name={previewNoteFields[previewLocale]}
                    rows={4}
                    value={previewDraft[previewNoteFields[previewLocale]]}
                    disabled={previewTranslationLocked}
                    onChange={(event) => setPreviewDraft((draft) => ({ ...draft, [previewNoteFields[previewLocale]]: event.target.value }))}
                    required={previewLocale === "zh" || previewLocale === "en"}
                  />
                </label>
              </div>

              <section className="previewBulkImport" aria-labelledby="preview-bulk-title">
                <div>
                  <strong id="preview-bulk-title">快速批量导入中文功能项</strong>
                  <small>每行一条；重新解析会保留精确匹配或唯一无歧义条目的 ID 与进度，中文改写后会清空旧译文。</small>
                </div>
                <textarea aria-label="快速批量导入中文功能项" rows={5} value={previewBulkInput} disabled={previewTranslationLocked} onChange={(event) => setPreviewBulkInput(event.target.value)} placeholder={"新增省电模式，进一步降低后台资源占用。\n新增逐字跟随，让歌词随演唱进度逐字呈现。\n新增歌词坞，让当前歌词直接呈现在 Windows 任务栏。"} />
                <div className="previewBulkActions"><button className="button buttonSecondary" type="button" disabled={previewTranslationLocked} onClick={parsePreviewBulkInput}>解析为功能项</button><button className="button buttonSecondary" type="button" disabled={previewTranslationLocked} onClick={addPreviewFeature}>新增单项</button></div>
              </section>

              <div className="previewFeatureList">
                {previewDraft.features.map((feature, index) => (
                  <article className="previewFeatureEditor" key={feature.id}>
                    <header>
                      <strong>功能 {index + 1}</strong>
                      <div className="previewFeatureActions">
                        <button type="button" disabled={previewTranslationLocked || index === 0} onClick={() => movePreviewFeature(feature.id, -1)}>上移</button>
                        <button type="button" disabled={previewTranslationLocked || index === previewDraft.features.length - 1} onClick={() => movePreviewFeature(feature.id, 1)}>下移</button>
                        <button className="danger" type="button" disabled={previewTranslationLocked} onClick={() => removePreviewFeature(feature.id)}>删除</button>
                      </div>
                    </header>
                    <label>
                      <span>{previewLocaleOptions.find((option) => option.value === previewLocale)?.label} 内容</span>
                      <textarea rows={3} value={feature[previewFeatureFields[previewLocale]]} disabled={previewTranslationLocked} onChange={(event) => updatePreviewFeature(feature.id, { [previewFeatureFields[previewLocale]]: event.target.value })} required={previewLocale === "zh" || previewLocale === "en"} />
                    </label>
                    <div className="previewProgressEditor">
                      <label className="previewProgressSelect">
                        <span>开发进度与状态</span>
                        <input
                          type="range"
                          min={0}
                          max={RELEASE_PREVIEW_SLIDER_READY_VALUE}
                          step={1}
                          value={feature.stage === "ready"
                            ? RELEASE_PREVIEW_SLIDER_READY_VALUE
                            : feature.stage === "testing"
                              ? RELEASE_PREVIEW_SLIDER_TESTING_VALUE
                              : feature.progress}
                          list={`preview-progress-stops-${feature.id}`}
                          disabled={previewTranslationLocked}
                          onChange={(event) => {
                            const value = Number(event.target.value);
                            updatePreviewFeature(feature.id, value === RELEASE_PREVIEW_SLIDER_READY_VALUE
                              ? { progress: 100, stage: "ready" }
                              : value === RELEASE_PREVIEW_SLIDER_TESTING_VALUE
                                ? { progress: 100, stage: "testing" }
                                : { progress: value, stage: "development" });
                          }}
                          aria-label={`功能 ${index + 1} 开发进度与状态`}
                          aria-valuetext={feature.stage === "ready" ? "测试完成，待上线" : feature.stage === "testing" ? "开发完成，待测试" : `${feature.progress}%`}
                        />
                        <datalist id={`preview-progress-stops-${feature.id}`}>
                          <option value="0" label="未开始" />
                          <option value="100" label="100%" />
                          <option value={RELEASE_PREVIEW_SLIDER_TESTING_VALUE} label="待测试" />
                          <option value={RELEASE_PREVIEW_SLIDER_READY_VALUE} label="待上线" />
                        </datalist>
                      </label>
                      <ReleasePreviewProgressRing progress={feature.progress} stage={feature.stage} locale="zh" decorative />
                      <span className="previewProgressEditorStatus" aria-live="polite">
                        {feature.stage === "ready" ? "测试完成，待上线" : feature.stage === "testing" ? "开发完成，待测试" : feature.progress === 0 ? "未开始" : `开发中 · ${feature.progress}%`}
                      </span>
                    </div>
                  </article>
                ))}
                {!previewDraft.features.length && <p className="previewFeatureEmpty">粘贴中文内容并解析，或新增单项开始编辑。</p>}
              </div>

              <div className="previewEditorActions">
                <button className="button buttonSecondary" type="button" disabled={previewSaving || translationLocked} onClick={() => void translatePreview("all")}>{translationSaving === "preview" ? "正在翻译…" : "翻译全部"}</button>
                <button className="button buttonSecondary" type="button" disabled={previewSaving || translationLocked} onClick={() => void translatePreview("missing")}>仅翻译缺失内容</button>
                <button className="button buttonSecondary" type="submit" name="intent" value="save" disabled={previewSaving || previewTranslationLocked}>{previewDraft.status === "published" ? "保存更改" : "保存草稿"}</button>
                <button className="button buttonPrimary" type="submit" name="intent" value="published" disabled={previewSaving || previewTranslationLocked}>{previewSaving ? "正在保存…" : previewDraft.status === "published" ? "保持发布并保存" : "发布预告"}</button>
              </div>
            </form>
            <div className="previewAdminList">{previews.map((preview) => <article key={preview.id}><div><span className="published">已发布到前台</span><small>{preview.version} · 预计上线：{formatReleaseTiming(preview.target_date, "zh")}</small><p>Note：{preview.note_zh || "未填写"}</p><p>功能项：{preview.features.length} 条</p></div><div><button className="button buttonSecondary" type="button" onClick={() => editPreview(preview)}>编辑</button><button className="button buttonSecondary" type="button" onClick={() => void togglePreview(preview)}>撤回为草稿</button></div></article>)}</div>
          </>
        )}

        {panel === "access" && (
          <>
            <header className="adminPageHeader">
              <div><p>ACCESS AUDIT</p><h2>访问日志</h2></div>
            </header>
            <div className="adminFilters accessFilters" style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "end", marginBottom: 18 }}>
              <label><span>搜索 IP / 路径 / 来源 / 设备</span><input style={{ ...accessFilterInputStyle, minWidth: 280 }} type="search" value={logQuery} onChange={(event) => setLogQuery(event.target.value)} placeholder="输入 IP、国家、URL、UA…" /></label>
              <label><span>访问事件</span><select value={eventFilter} onChange={(event) => setEventFilter(event.target.value)}><option value="all">全部事件</option>{eventOptions.map((eventType) => <option value={eventType} key={eventType}>{eventLabels[eventType] ?? eventType}</option>)}</select></label>
              <label><span>访问范围</span><select value={scopeFilter} onChange={(event) => setScopeFilter(event.target.value as typeof scopeFilter)}><option value="all">前后台全部</option><option value="public">仅前台</option><option value="admin">仅后台</option></select></label>
              <label><span>风险级别</span><select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value as typeof severityFilter)}><option value="all">全部级别</option><option value="normal">正常</option><option value="warning">异常</option><option value="critical">高风险</option></select></label>
              <label><span>请求方法</span><select value={methodFilter} onChange={(event) => setMethodFilter(event.target.value)}><option value="all">全部方法</option>{methodOptions.map((method) => <option key={method} value={method}>{method}</option>)}</select></label>
              <label><span>国家 / 地区</span><select value={countryFilter} onChange={(event) => setCountryFilter(event.target.value)}><option value="all">全部地区</option>{countryOptions.map((country) => <option key={country} value={country}>{country}</option>)}</select></label>
              <label><span>设备类型</span><select value={deviceFilter} onChange={(event) => setDeviceFilter(event.target.value as typeof deviceFilter)}><option value="all">全部设备</option><option value="desktop">桌面设备</option><option value="mobile">移动设备</option><option value="bot">机器人 / 脚本</option><option value="unknown">未知设备</option></select></label>
              <label><span>开始时间</span><input style={accessFilterInputStyle} type="datetime-local" value={logDateFrom} onChange={(event) => setLogDateFrom(event.target.value)} /></label>
              <label><span>结束时间</span><input style={accessFilterInputStyle} type="datetime-local" value={logDateTo} onChange={(event) => setLogDateTo(event.target.value)} /></label>
              <button className="button buttonSecondary" type="button" onClick={clearLogFilters}>清除筛选</button>
            </div>
            <div className="accessSummary"><div><strong>{accessLogs.filter((item) => item.scope === "public").length}</strong><span>前台记录</span></div><div><strong>{accessLogs.filter((item) => item.scope === "admin").length}</strong><span>后台记录</span></div><div className={unreadAlerts ? "hasAlert" : ""}><strong>{unreadAlerts}</strong><span>未确认异常</span></div></div>
            <section className="severityRules" aria-label="异常事件判定规则">
              <div><span className="severityBadge">正常</span><strong>合法成功请求</strong><p>普通页面访问、用户提交反馈、登录成功，以及已登录管理员的查看、更新和删除操作。</p></div>
              <div><span className="severityBadge warning">异常</span><strong>校验失败，暂未确认恶意</strong><p>密码错误，或同站点请求缺少有效管理员会话。可能是登录过期或误操作，需要留意重复频率。</p></div>
              <div><span className="severityBadge critical">高风险</span><strong>明确跨站或越权特征</strong><p>外部来源向后台登录、修改或删除接口发起请求，具备跨站请求或主动探测特征，应优先检查。</p></div>
            </section>
            <div className="accessResultMeta">当前显示 <strong>{visibleLogs.length}</strong> / {accessLogs.length} 条；“未确认异常”仅统计异常和高风险事件。</div>
            <div className="adminTableWrap">
              <table className="adminDataTable accessTable">
                <colgroup><col className="accessTimeColumn" /><col className="accessEventColumn" /><col className="accessDetailColumn" /><col className="accessVisitorColumn" /><col className="accessDeviceColumn" /></colgroup>
                <thead><tr><th>时间 / 级别</th><th>访问事件 / 接口</th><th>具体内容</th><th>访客</th><th>设备与来源</th></tr></thead>
                <tbody>{visibleLogs.map((item) => (
                  <tr className={`severity-${item.severity}`} key={item.id}>
                    <td><time>{formatDate(item.created_at)}</time><span className={`severityBadge ${item.severity}`} title={item.severity === "critical" ? "明确跨站或越权特征" : item.severity === "warning" ? "校验失败，暂未确认恶意" : "合法成功请求"}>{item.severity === "critical" ? "高风险" : item.severity === "warning" ? "异常" : "正常"}{item.acknowledged_at ? " · 已确认" : ""}</span></td>
                    <td><strong>{eventLabels[item.event_type] ?? item.event_type}</strong><code title={item.path}>{item.path}</code><small>{item.scope === "admin" ? "后台" : "前台"} · {item.method} {item.status_code ?? "—"}</small></td>
                    <td><AccessEventDetail item={item} /></td>
                    <td><code title={item.ip_source ? `来源字段：${item.ip_source}` : undefined}>{item.ip_address ?? "IP 未记录"}</code><small>{[item.country, item.region, item.city].filter(Boolean).join(" · ") || "未知地区"}</small><small title={item.visitor_hash}>访客标识：{item.visitor_hash.slice(0, 12)}</small></td>
                    <td><strong>{deviceSummary(item.user_agent)}</strong><small>{item.accept_language ? `语言：${item.accept_language}` : "语言未知"}{item.request_id ? ` · 请求 ${item.request_id.slice(0, 12)}` : ""}</small><small title={item.referrer ?? undefined}>{item.referrer ? `来源：${item.referrer}` : "直接访问"}</small><small title={item.user_agent ?? undefined}>{item.user_agent ?? "未记录 User-Agent"}</small></td>
                  </tr>
                ))}</tbody>
              </table>
              {!visibleLogs.length && <p className="adminEmpty">当前筛选条件下没有访问记录。</p>}
            </div>
          </>
        )}

        {panel === "promo-codes" && (
          <AdminPromoCodes embedded />
        )}
      </main>

      {editing && (
        <div className="adminModalBackdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setEditing(null); }}>
          <section className="adminEditModal" role="dialog" aria-modal="true" aria-labelledby="edit-feedback-title">
            <header><div><p>EDIT FEEDBACK</p><h2 id="edit-feedback-title">更改反馈内容</h2></div><button aria-label="关闭" onClick={() => setEditing(null)}>×</button></header>
            <div className="editFormGrid">
              <label><span>类型</span><select value={editing.kind} onChange={(event) => setEditing({ ...editing, kind: event.target.value as IncentiveSubmission["kind"] })}><option value="feature">新功能</option><option value="bug">Bug</option></select></label>
              <label><span>提交者昵称</span><input value={editing.nickname} onChange={(event) => setEditing({ ...editing, nickname: event.target.value })} /></label>
              <label className="wide"><span>联系邮箱</span><input type="email" value={editing.email} onChange={(event) => setEditing({ ...editing, email: event.target.value })} /></label>
              <label className="wide"><span>标题</span><input value={editing.title} onChange={(event) => setEditing({ ...editing, title: event.target.value })} /></label>
              <label className="wide"><span>内容</span><textarea rows={7} value={editing.body} onChange={(event) => setEditing({ ...editing, body: event.target.value })} /></label>
              <label><span>审阅状态</span><select value={editing.status} onChange={(event) => setEditing({ ...editing, status: event.target.value as SubmissionStatus, ...(event.target.value === "accepted" ? {} : { is_public: false }) })}>{Object.entries(statusLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
              <label><span>奖励状态</span><select value={editing.reward_status} onChange={(event) => setEditing({ ...editing, reward_status: event.target.value as RewardStatus })}>{Object.entries(rewardLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
              <label><span>点赞数</span><input type="number" min={0} step={1} value={editing.like_count} onChange={(event) => setEditing({ ...editing, like_count: Math.max(0, Math.trunc(Number(event.target.value) || 0)) })} /></label>
              <label><span>提交时间</span><input type="datetime-local" value={toDateTimeLocal(editing.created_at)} onChange={(event) => { const date = new Date(event.target.value); if (!Number.isNaN(date.getTime())) setEditing({ ...editing, created_at: date.toISOString() }); }} /></label>
              <label className="wide"><span>开发者回复</span><textarea rows={4} value={editing.developer_reply ?? ""} onChange={(event) => setEditing({ ...editing, developer_reply: event.target.value })} /></label>
              <div className="editChecks wide"><label><input type="checkbox" checked={editing.is_public} onChange={(event) => setEditing({ ...editing, is_public: event.target.checked, ...(event.target.checked ? { status: "accepted" } : {}) })} />在前台展出</label><label><input type="checkbox" checked={editing.is_flagged} onChange={(event) => setEditing({ ...editing, is_flagged: event.target.checked })} />红旗标注</label></div>
            </div>
            <footer><button className="button buttonSecondary danger" onClick={() => void removeSubmission(editing)}>删除反馈</button><div><button className="button buttonSecondary" onClick={() => setEditing(null)}>取消</button><button className="button buttonPrimary" disabled={savingId === editing.id} onClick={async () => { if (await saveSubmission(editing)) setEditing(null); }}>{savingId === editing.id ? "保存中…" : "保存更改"}</button></div></footer>
          </section>
        </div>
      )}
    </div>
  );
}
