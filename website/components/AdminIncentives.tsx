"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { ExternalArrow } from "@/components/ExternalArrow";
import { LogoLockup } from "@/components/SitePage";
import {
  defaultFeatureContent,
  sanitizeFeatureContent
} from "@/data/feature-content";
import type {
  AccessLogEntry,
  AccessSeverity,
  FeatureContent,
  FeatureContentSection,
  IncentiveSubmission,
  ReleasePreview,
  RewardStatus,
  SubmissionStatus
} from "@/data/incentives-types";

type AuthState = "checking" | "login" | "ready";
type Panel = "submissions" | "features" | "previews" | "access";
type SaveFeedback = { tone: "pending" | "success" | "error"; message: string };
type PreviewDraft = {
  id?: string;
  version: string;
  body_zh: string;
  body_en: string;
  target_date: string;
  status: "draft" | "published";
};
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
  body_zh: "",
  body_en: "",
  target_date: "",
  status: "draft"
};

function previewToDraft(preview: ReleasePreview): PreviewDraft {
  return {
    id: preview.id,
    version: preview.version,
    body_zh: [preview.body_zh, ...preview.highlights_zh].filter(Boolean).join("\n"),
    body_en: [preview.body_en, ...preview.highlights_en].filter(Boolean).join("\n"),
    target_date: preview.target_date ?? "",
    status: preview.status
  };
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

  return (
    <div className="accessEventDetail">
      <strong>{eventDescriptions[item.event_type] ?? "系统审计事件"}</strong>
      {item.event_type === "page_view" && typeof item.details.authenticated === "boolean" && (
        <small>{item.details.authenticated ? "访问时后台会话有效" : "访问时未登录后台"}</small>
      )}
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
  const [previews, setPreviews] = useState<ReleasePreview[]>([]);
  const [accessLogs, setAccessLogs] = useState<AccessLogEntry[]>([]);
  const [unreadAlerts, setUnreadAlerts] = useState(0);
  const [kindFilter, setKindFilter] = useState<"all" | "feature" | "bug">("all");
  const [statusFilter, setStatusFilter] = useState<"all" | SubmissionStatus>("all");
  const [severityFilter, setSeverityFilter] = useState<"all" | AccessSeverity>("all");
  const [scopeFilter, setScopeFilter] = useState<"all" | "public" | "admin">("all");
  const [eventFilter, setEventFilter] = useState("all");
  const [panel, setPanel] = useState<Panel>("submissions");
  const [viewMode, setViewMode] = useState<"table" | "cards">("table");
  const [savingId, setSavingId] = useState("");
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [bulkSaving, setBulkSaving] = useState(false);
  const [bulkMessage, setBulkMessage] = useState("");
  const [saveFeedback, setSaveFeedback] = useState<Record<string, SaveFeedback>>({});
  const [editing, setEditing] = useState<IncentiveSubmission | null>(null);
  const [previewDateTbd, setPreviewDateTbd] = useState(false);
  const [previewDraft, setPreviewDraft] = useState<PreviewDraft>(emptyPreviewDraft);
  const [previewSaving, setPreviewSaving] = useState(false);

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
    const previewData = (await previewResponse.json()) as { previews: ReleasePreview[] };
    const logData = logResponse.ok
      ? await logResponse.json() as { logs: AccessLogEntry[]; unreadAlerts: number }
      : { logs: [] as AccessLogEntry[], unreadAlerts: 0 };
    setSubmissions(submissionData.submissions);
    setFeatureContent(sanitizeFeatureContent(featureData.content));
    setPreviews(previewData.previews);
    const currentPreview = previewData.previews.find((preview) => preview.status === "published")
      ?? previewData.previews[0];
    if (currentPreview) {
      setPreviewDraft(previewToDraft(currentPreview));
      setPreviewDateTbd(!currentPreview.target_date);
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

  const visibleLogs = useMemo(() => accessLogs.filter((item) =>
    (scopeFilter === "all" || item.scope === scopeFilter) &&
    (severityFilter === "all" || item.severity === severityFilter) &&
    (eventFilter === "all" || item.event_type === eventFilter)
  ), [accessLogs, scopeFilter, severityFilter, eventFilter]);

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

  async function savePreview(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
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
          body_zh: previewDraft.body_zh,
          body_en: previewDraft.body_en,
          target_date: previewDateTbd ? "" : previewDraft.target_date,
          status
        })
      });
      const result = (await response.json()) as { error?: string; preview?: ReleasePreview };
      if (!response.ok || !result.preview) {
        setError(result.error ?? "保存失败");
        return;
      }
      setPreviews((items) => items.some((item) => item.id === result.preview!.id)
        ? items.map((item) => item.id === result.preview!.id ? result.preview! : item)
        : [result.preview!, ...items]);
      setPreviewDraft(previewToDraft(result.preview));
      setPreviewDateTbd(!result.preview.target_date);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "保存失败");
    } finally {
      setPreviewSaving(false);
    }
  }

  function editPreview(preview: ReleasePreview) {
    setPreviewDraft(previewToDraft(preview));
    setPreviewDateTbd(!preview.target_date);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function newPreview() {
    setPreviewDraft(emptyPreviewDraft);
    setPreviewDateTbd(true);
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
    setPreviews((items) => items.map((item) => item.id === preview.id ? result.preview! : item));
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
      const target = index + direction;
      if (index < 0 || target < 0 || target >= content.sections.length) return content;
      const sections = [...content.sections];
      [sections[index], sections[target]] = [sections[target], sections[index]];
      return { ...content, sections };
    });
    markFeatureDirty();
  }

  function addFeatureSection() {
    const id = typeof crypto !== "undefined" && "randomUUID" in crypto
      ? crypto.randomUUID()
      : `feature-${Date.now()}`;
    setFeatureContent((content) => ({
      ...content,
      sections: [...content.sections, {
        id,
        title_zh: "",
        title_en: "",
        body_zh: "",
        body_en: "",
        items_zh: [],
        items_en: [],
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
      <aside className="adminSidebar">
        <LogoLockup />
        <p className="eyebrow"><span aria-hidden="true">•</span>维护者后台</p>
        <h1>用户反馈</h1>
        <nav aria-label="后台导航">
          <button className={panel === "submissions" ? "isActive" : ""} onClick={() => setPanel("submissions")}>反馈管理 {pendingSubmissionCount > 0 && <span>{pendingSubmissionCount}</span>}</button>
          <button className={panel === "features" ? "isActive" : ""} onClick={() => setPanel("features")}>新功能内容</button>
          <button className={panel === "previews" ? "isActive" : ""} onClick={() => setPanel("previews")}>版本预告</button>
          <button className={panel === "access" ? "isActive" : ""} onClick={() => setPanel("access")}>访问日志 {unreadAlerts > 0 && <span className="alertCount">{unreadAlerts}</span>}</button>
        </nav>
        <div className="adminSidebarBottom">
          <Link href="/incentives" target="_blank">查看前台 <ExternalArrow /></Link>
          <Link href="/updates" target="_blank">查看新功能页 <ExternalArrow /></Link>
          <button onClick={logout}>退出登录</button>
        </div>
      </aside>

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
                <span role="status">{featureMessage || "现有中英文内容已导入，可直接修改"}</span>
                <button className="button buttonSecondary" type="button" onClick={addFeatureSection}>新增功能条目</button>
                <button className="button buttonPrimary" type="button" disabled={featureSaving} onClick={() => void saveManagedFeatures()}>{featureSaving ? "保存中…" : "保存并同步前台"}</button>
              </div>
            </header>

            <section className="featureAdminSummary">
              <header>
                <div><strong>本次重点</strong><small>管理顶部重点摘要，中文和英文同步保存</small></div>
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
              </div>
            </section>

            <div className="featureAdminList">
              {featureContent.sections.map((section, index) => (
                <article key={section.id}>
                  <header>
                    <div><span>{String(index + 1).padStart(2, "0")}</span><strong>{section.title_zh || section.title_en || "未命名功能条目"}</strong></div>
                    <div className="featureRowActions">
                      <button type="button" disabled={index === 0} onClick={() => moveFeatureSection(section.id, -1)}>上移</button>
                      <button type="button" disabled={index === featureContent.sections.length - 1} onClick={() => moveFeatureSection(section.id, 1)}>下移</button>
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
                  </div>
                </article>
              ))}
              {!featureContent.sections.length && <p className="adminEmpty">还没有功能条目，请先新增一条再保存。</p>}
            </div>
          </>
        )}

        {panel === "previews" && (
          <>
            <header className="adminPageHeader"><div><p>RELEASE PREVIEW</p><h2>发布版本预告</h2></div><button className="button buttonSecondary" type="button" onClick={newPreview}>新建预告</button></header>
            <form className="previewEditor" onSubmit={savePreview}>
              <div className="previewEditorMeta"><label><span>版本号</span><input name="version" placeholder="例如：v2.1 Beta" value={previewDraft.version} onChange={(event) => setPreviewDraft((draft) => ({ ...draft, version: event.target.value }))} required /></label><label><span>预计上线时间</span><input name="target_date" type="date" value={previewDraft.target_date} onChange={(event) => setPreviewDraft((draft) => ({ ...draft, target_date: event.target.value }))} disabled={previewDateTbd} /></label><button className={`previewDateTbdButton ${previewDateTbd ? "isActive" : ""}`} type="button" aria-pressed={previewDateTbd} onClick={() => setPreviewDateTbd((current) => !current)}>上线时间待定</button></div>
              <div className="previewEditorLanguages"><label><span>更新内容（中文）</span><textarea name="body_zh" rows={9} value={previewDraft.body_zh} onChange={(event) => setPreviewDraft((draft) => ({ ...draft, body_zh: event.target.value }))} required /></label><label><span>Update content (English)</span><textarea name="body_en" rows={9} value={previewDraft.body_en} onChange={(event) => setPreviewDraft((draft) => ({ ...draft, body_en: event.target.value }))} required /></label></div>
              <div className="previewEditorActions"><button className="button buttonSecondary" type="submit" name="intent" value="save" disabled={previewSaving}>{previewDraft.status === "published" ? "保存更改" : "保存草稿"}</button><button className="button buttonPrimary" type="submit" name="intent" value="published" disabled={previewSaving}>{previewSaving ? "正在保存…" : previewDraft.status === "published" ? "保持发布并保存" : "发布预告"}</button></div>
            </form>
            <div className="previewAdminList">{previews.map((preview) => <article key={preview.id}><div><span className={preview.status}>{preview.status === "published" ? "已发布" : "草稿"}</span><small>{preview.version} · 预计上线：{preview.target_date ?? "待定"}</small><p>中文：{[preview.body_zh, ...preview.highlights_zh].filter(Boolean).join(" / ")}</p>{preview.body_en && <p>English: {[preview.body_en, ...preview.highlights_en].filter(Boolean).join(" / ")}</p>}</div><div><button className="button buttonSecondary" type="button" onClick={() => editPreview(preview)}>编辑</button><button className="button buttonSecondary" type="button" onClick={() => void togglePreview(preview)}>{preview.status === "published" ? "撤回为草稿" : "发布到前台"}</button></div></article>)}</div>
          </>
        )}

        {panel === "access" && (
          <>
            <header className="adminPageHeader">
              <div><p>ACCESS AUDIT</p><h2>访问日志</h2></div>
              <div className="adminFilters accessFilters">
                <label><span>访问事件</span><select value={eventFilter} onChange={(event) => setEventFilter(event.target.value)}><option value="all">全部事件</option>{eventOptions.map((eventType) => <option value={eventType} key={eventType}>{eventLabels[eventType] ?? eventType}</option>)}</select></label>
                <label><span>访问范围</span><select value={scopeFilter} onChange={(event) => setScopeFilter(event.target.value as typeof scopeFilter)}><option value="all">前后台全部</option><option value="public">仅前台</option><option value="admin">仅后台</option></select></label>
                <label><span>风险级别</span><select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value as typeof severityFilter)}><option value="all">全部级别</option><option value="normal">正常</option><option value="warning">异常</option><option value="critical">高风险</option></select></label>
              </div>
            </header>
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
                    <td><code title={item.visitor_hash}>{item.visitor_hash.slice(0, 12)}</code><small>{item.country ?? "未知地区"}</small></td>
                    <td><strong>{deviceSummary(item.user_agent)}</strong><small title={item.referrer ?? undefined}>{item.referrer ? `来源：${item.referrer}` : "直接访问"}</small></td>
                  </tr>
                ))}</tbody>
              </table>
              {!visibleLogs.length && <p className="adminEmpty">当前筛选条件下没有访问记录。</p>}
            </div>
          </>
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
