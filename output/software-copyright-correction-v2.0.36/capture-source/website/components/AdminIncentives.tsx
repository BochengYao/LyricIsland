"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { ExternalArrow } from "@/components/ExternalArrow";
import { LogoLockup } from "@/components/SitePage";
import type {
  AccessLogEntry,
  AccessSeverity,
  IncentiveSubmission,
  ReleasePreview,
  RewardStatus,
  SubmissionStatus
} from "@/data/incentives-types";

type AuthState = "checking" | "login" | "ready";
type Panel = "submissions" | "previews" | "access";
type SaveFeedback = { tone: "pending" | "success" | "error"; message: string };

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

const eventLabels: Record<string, string> = {
  page_view: "页面访问",
  login_succeeded: "后台登录成功",
  login_failed: "后台登录失败",
  cross_origin_login_attempt: "跨站登录尝试",
  submission_updated: "更新反馈",
  submission_deleted: "删除反馈",
  security_alerts_acknowledged: "确认安全提醒",
  unauthorized_submission_delete: "未授权删除尝试",
  unauthorized_submission_update: "未授权修改尝试",
  unauthorized_alert_acknowledge: "未授权确认提醒"
};

function formatDate(value: string) {
  return new Date(value).toLocaleString("zh-CN", { hour12: false });
}

function deviceSummary(userAgent: string | null) {
  if (!userAgent) return "未知设备";
  const browser = userAgent.includes("Edg/") ? "Edge" : userAgent.includes("Chrome/") ? "Chrome" : userAgent.includes("Firefox/") ? "Firefox" : userAgent.includes("Safari/") ? "Safari" : "其他浏览器";
  const system = userAgent.includes("Windows") ? "Windows" : userAgent.includes("Android") ? "Android" : userAgent.includes("iPhone") || userAgent.includes("iPad") ? "iOS" : userAgent.includes("Mac OS") ? "macOS" : "未知系统";
  return `${system} · ${browser}`;
}

export function AdminIncentives() {
  const [auth, setAuth] = useState<AuthState>("checking");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submissions, setSubmissions] = useState<IncentiveSubmission[]>([]);
  const [previews, setPreviews] = useState<ReleasePreview[]>([]);
  const [accessLogs, setAccessLogs] = useState<AccessLogEntry[]>([]);
  const [unreadAlerts, setUnreadAlerts] = useState(0);
  const [kindFilter, setKindFilter] = useState<"all" | "feature" | "bug">("all");
  const [statusFilter, setStatusFilter] = useState<"all" | SubmissionStatus>("all");
  const [severityFilter, setSeverityFilter] = useState<"all" | AccessSeverity>("all");
  const [scopeFilter, setScopeFilter] = useState<"all" | "public" | "admin">("all");
  const [panel, setPanel] = useState<Panel>("submissions");
  const [viewMode, setViewMode] = useState<"table" | "cards">("table");
  const [savingId, setSavingId] = useState("");
  const [saveFeedback, setSaveFeedback] = useState<Record<string, SaveFeedback>>({});
  const [editing, setEditing] = useState<IncentiveSubmission | null>(null);
  const [previewDateTbd, setPreviewDateTbd] = useState(false);

  async function loadData() {
    const [submissionResponse, previewResponse, logResponse] = await Promise.all([
      fetch("/api/incentives/admin/submissions", { cache: "no-store" }),
      fetch("/api/incentives/admin/previews", { cache: "no-store" }),
      fetch("/api/incentives/admin/access-logs", { cache: "no-store" })
    ]);
    if ([submissionResponse, previewResponse, logResponse].some((response) => response.status === 401)) {
      setAuth("login");
      return;
    }
    if (!submissionResponse.ok || !previewResponse.ok) throw new Error("后台数据读取失败");
    const submissionData = (await submissionResponse.json()) as { submissions: IncentiveSubmission[] };
    const previewData = (await previewResponse.json()) as { previews: ReleasePreview[] };
    const logData = logResponse.ok
      ? await logResponse.json() as { logs: AccessLogEntry[]; unreadAlerts: number }
      : { logs: [] as AccessLogEntry[], unreadAlerts: 0 };
    setSubmissions(submissionData.submissions);
    setPreviews(previewData.previews);
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
          is_public: next.is_public
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

  const visibleLogs = useMemo(() => accessLogs.filter((item) =>
    (scopeFilter === "all" || item.scope === scopeFilter) &&
    (severityFilter === "all" || item.severity === severityFilter)
  ), [accessLogs, scopeFilter, severityFilter]);

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

  async function createPreview(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    const form = new FormData(event.currentTarget);
    const response = await fetch("/api/incentives/admin/previews", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        version: form.get("version"),
        body_zh: form.get("body_zh"),
        body_en: form.get("body_en"),
        target_date: previewDateTbd ? "" : form.get("target_date"),
        status: form.get("intent")
      })
    });
    const result = (await response.json()) as { error?: string; preview?: ReleasePreview };
    if (!response.ok || !result.preview) {
      setError(result.error ?? "发布失败");
      return;
    }
    setPreviews((items) => [result.preview!, ...items]);
    event.currentTarget.reset();
    setPreviewDateTbd(false);
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
  }

  if (auth === "checking") return <main className="adminLoading">正在确认后台身份…</main>;

  if (auth === "login") {
    return (
      <main className="adminLoginPage">
        <section className="adminLoginCard">
          <LogoLockup />
          <p className="eyebrow"><span aria-hidden="true">•</span>维护者后台</p>
          <h1>审阅用户提交</h1>
          <p>登录后可以管理反馈、版本预告与前后台访问日志。</p>
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
          <button className={panel === "previews" ? "isActive" : ""} onClick={() => setPanel("previews")}>版本预告</button>
          <button className={panel === "access" ? "isActive" : ""} onClick={() => setPanel("access")}>访问日志 {unreadAlerts > 0 && <span className="alertCount">{unreadAlerts}</span>}</button>
        </nav>
        <div className="adminSidebarBottom">
          <Link href="/incentives" target="_blank">查看前台 <ExternalArrow /></Link>
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
                  <select value={kindFilter} onChange={(event) => setKindFilter(event.target.value as typeof kindFilter)} aria-label="提交类型"><option value="all">全部类型</option><option value="feature">新功能</option><option value="bug">Bug</option></select>
                  <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} aria-label="审阅状态"><option value="all">全部状态</option>{Object.entries(statusLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>
                </div>
              </div>
            </header>

            {viewMode === "table" ? (
              <div className="adminTableWrap">
                <table className="adminDataTable feedbackTable">
                  <thead><tr><th>反馈</th><th>提交者</th><th>状态</th><th>前台展出</th><th>更新时间</th><th>操作</th></tr></thead>
                  <tbody>{visibleSubmissions.map((item) => (
                    <tr className={item.is_flagged ? "isFlagged" : ""} key={item.id}>
                      <td><div className="tableTitle"><span className={`kindBadge ${item.kind}`}>{item.kind === "feature" ? "新功能" : "Bug"}</span><strong>{item.title}</strong></div><p>{item.body}</p>{saveFeedback[item.id] && <small className={saveFeedback[item.id].tone}>{saveFeedback[item.id].message}</small>}</td>
                      <td><strong>@{item.nickname}</strong><a href={`mailto:${item.email}`}>{item.email}</a></td>
                      <td><select value={item.status} disabled={savingId === item.id} onChange={(event) => void quickUpdate(item, { status: event.target.value as SubmissionStatus, ...(event.target.value === "accepted" ? {} : { is_public: false }) })}>{Object.entries(statusLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></td>
                      <td><button className={`displayToggle ${item.is_public ? "isPublic" : ""}`} disabled={savingId === item.id} onClick={() => void quickUpdate(item, item.is_public ? { is_public: false } : { status: "accepted", is_public: true })} aria-pressed={item.is_public}><span aria-hidden="true" />{item.is_public ? "已展出" : "未展出"}</button></td>
                      <td><time>{formatDate(item.updated_at)}</time></td>
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
                    <header><div><span className={`kindBadge ${item.kind}`}>{item.kind === "feature" ? "新功能" : "Bug"}</span><time>{formatDate(item.created_at)}</time>{item.kind === "feature" && <span>♥ {item.like_count}</span>}</div><strong>{item.title}</strong></header>
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

        {panel === "previews" && (
          <>
            <header className="adminPageHeader"><div><p>RELEASE PREVIEW</p><h2>发布版本预告</h2></div></header>
            <form className="previewEditor" onSubmit={createPreview}>
              <div className="previewEditorMeta"><label><span>版本号</span><input name="version" placeholder="例如：v2.1 Beta" required /></label><label><span>预计上线时间</span><input name="target_date" type="date" disabled={previewDateTbd} /></label><button className={`previewDateTbdButton ${previewDateTbd ? "isActive" : ""}`} type="button" aria-pressed={previewDateTbd} onClick={() => setPreviewDateTbd((current) => !current)}>上线时间待定</button></div>
              <div className="previewEditorLanguages"><label><span>更新内容（中文）</span><textarea name="body_zh" rows={9} required /></label><label><span>Update content (English)</span><textarea name="body_en" rows={9} required /></label></div>
              <div className="previewEditorActions"><button className="button buttonSecondary" type="submit" name="intent" value="draft">保存草稿</button><button className="button buttonPrimary" type="submit" name="intent" value="published">发布预告</button></div>
            </form>
            <div className="previewAdminList">{previews.map((preview) => <article key={preview.id}><div><span className={preview.status}>{preview.status === "published" ? "已发布" : "草稿"}</span><small>{preview.version} · 预计上线：{preview.target_date ?? "待定"}</small><p>中文：{preview.body_zh}</p>{preview.body_en && <p>English: {preview.body_en}</p>}</div><button className="button buttonSecondary" onClick={() => void togglePreview(preview)}>{preview.status === "published" ? "撤回为草稿" : "发布到前台"}</button></article>)}</div>
          </>
        )}

        {panel === "access" && (
          <>
            <header className="adminPageHeader"><div><p>ACCESS AUDIT</p><h2>访问日志</h2></div><div className="adminFilters"><select value={scopeFilter} onChange={(event) => setScopeFilter(event.target.value as typeof scopeFilter)}><option value="all">前后台全部</option><option value="public">仅前台</option><option value="admin">仅后台</option></select><select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value as typeof severityFilter)}><option value="all">全部级别</option><option value="normal">正常</option><option value="warning">异常</option><option value="critical">高风险</option></select></div></header>
            <div className="accessSummary"><div><strong>{accessLogs.filter((item) => item.scope === "public").length}</strong><span>前台记录</span></div><div><strong>{accessLogs.filter((item) => item.scope === "admin").length}</strong><span>后台记录</span></div><div className={unreadAlerts ? "hasAlert" : ""}><strong>{unreadAlerts}</strong><span>未确认异常</span></div></div>
            <div className="adminTableWrap"><table className="adminDataTable accessTable"><thead><tr><th>时间 / 级别</th><th>访问事件</th><th>路径</th><th>访客</th><th>设备与来源</th></tr></thead><tbody>{visibleLogs.map((item) => <tr className={`severity-${item.severity}`} key={item.id}><td><time>{formatDate(item.created_at)}</time><span className={`severityBadge ${item.severity}`}>{item.severity === "critical" ? "高风险" : item.severity === "warning" ? "异常" : "正常"}{item.acknowledged_at ? " · 已确认" : ""}</span></td><td><strong>{eventLabels[item.event_type] ?? item.event_type}</strong><small>{item.scope === "admin" ? "后台" : "前台"} · {item.method} {item.status_code ?? "—"}</small></td><td><code>{item.path}</code></td><td><code title={item.visitor_hash}>{item.visitor_hash.slice(0, 12)}</code><small>{item.country ?? "未知地区"}</small></td><td><strong>{deviceSummary(item.user_agent)}</strong><small title={item.referrer ?? undefined}>{item.referrer ? `来源：${item.referrer}` : "直接访问"}</small></td></tr>)}</tbody></table>{!visibleLogs.length && <p className="adminEmpty">当前筛选条件下没有访问记录。</p>}</div>
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
