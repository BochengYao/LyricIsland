"use client";

import Link from "next/link";
import { ExternalArrow } from "@/components/ExternalArrow";
import { useEffect, useMemo, useState } from "react";
import { LogoLockup } from "@/components/SitePage";
import type {
  IncentiveSubmission,
  ReleasePreview,
  RewardStatus,
  SubmissionStatus
} from "@/data/incentives-types";

type AuthState = "checking" | "login" | "ready";

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

export function AdminIncentives() {
  const [auth, setAuth] = useState<AuthState>("checking");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submissions, setSubmissions] = useState<IncentiveSubmission[]>([]);
  const [previews, setPreviews] = useState<ReleasePreview[]>([]);
  const [kindFilter, setKindFilter] = useState<"all" | "feature" | "bug">("all");
  const [statusFilter, setStatusFilter] = useState<"all" | SubmissionStatus>("all");
  const [panel, setPanel] = useState<"submissions" | "previews">("submissions");
  const [savingId, setSavingId] = useState("");

  async function loadData() {
    const [submissionResponse, previewResponse] = await Promise.all([
      fetch("/api/incentives/admin/submissions", { cache: "no-store" }),
      fetch("/api/incentives/admin/previews", { cache: "no-store" })
    ]);
    if (submissionResponse.status === 401 || previewResponse.status === 401) {
      setAuth("login");
      return;
    }
    if (!submissionResponse.ok || !previewResponse.ok) throw new Error("后台数据读取失败");
    const submissionData = (await submissionResponse.json()) as { submissions: IncentiveSubmission[] };
    const previewData = (await previewResponse.json()) as { previews: ReleasePreview[] };
    setSubmissions(submissionData.submissions);
    setPreviews(previewData.previews);
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

  function editSubmission(id: string, patch: Partial<IncentiveSubmission>) {
    setSubmissions((items) => items.map((item) => item.id === id ? { ...item, ...patch } : item));
  }

  async function saveSubmission(item: IncentiveSubmission) {
    setSavingId(item.id);
    setError("");
    const response = await fetch("/api/incentives/admin/submissions", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        id: item.id,
        status: item.status,
        reward_status: item.reward_status,
        reviewer_note: item.reviewer_note ?? ""
      })
    });
    const result = (await response.json()) as { error?: string; submission?: IncentiveSubmission };
    setSavingId("");
    if (!response.ok || !result.submission) {
      setError(result.error ?? "保存失败");
      return;
    }
    editSubmission(item.id, result.submission);
  }

  const visibleSubmissions = useMemo(() => submissions.filter((item) =>
    (kindFilter === "all" || item.kind === kindFilter) &&
    (statusFilter === "all" || item.status === statusFilter)
  ), [submissions, kindFilter, statusFilter]);

  async function createPreview(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    const form = new FormData(event.currentTarget);
    const payload = {
      version: form.get("version"),
      title_zh: form.get("title_zh"),
      title_en: form.get("title_en"),
      body_zh: form.get("body_zh"),
      body_en: form.get("body_en"),
      target_date: form.get("target_date"),
      highlights_zh: String(form.get("highlights_zh") ?? "").split("\n"),
      highlights_en: String(form.get("highlights_en") ?? "").split("\n"),
      status: form.get("status")
    };
    const response = await fetch("/api/incentives/admin/previews", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const result = (await response.json()) as { error?: string; preview?: ReleasePreview };
    if (!response.ok || !result.preview) {
      setError(result.error ?? "发布失败");
      return;
    }
    setPreviews((items) => [result.preview!, ...items]);
    event.currentTarget.reset();
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

  if (auth === "checking") {
    return <main className="adminLoading">正在确认后台身份…</main>;
  }

  if (auth === "login") {
    return (
      <main className="adminLoginPage">
        <section className="adminLoginCard">
          <LogoLockup />
          <p className="eyebrow"><span aria-hidden="true">•</span>维护者后台</p>
          <h1>审阅用户提交</h1>
          <p>登录后可以处理新功能提议、Bug、奖励状态和版本预告。</p>
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
          <button className={panel === "submissions" ? "isActive" : ""} onClick={() => setPanel("submissions")}>
            审阅队列 <span>{submissions.filter((item) => item.status === "pending").length}</span>
          </button>
          <button className={panel === "previews" ? "isActive" : ""} onClick={() => setPanel("previews")}>版本预告</button>
        </nav>
        <div className="adminSidebarBottom">
          <Link href="/incentives" target="_blank">查看前台 <ExternalArrow /></Link>
          <button onClick={logout}>退出登录</button>
        </div>
      </aside>

      <main className="adminMain">
        {error && <p className="adminError" role="alert">{error}</p>}
        {panel === "submissions" ? (
          <>
            <header className="adminPageHeader">
              <div><p>SUBMISSIONS</p><h2>审阅队列</h2></div>
              <div className="adminFilters">
                <select value={kindFilter} onChange={(event) => setKindFilter(event.target.value as typeof kindFilter)} aria-label="提交类型">
                  <option value="all">全部类型</option><option value="feature">新功能</option><option value="bug">Bug</option>
                </select>
                <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} aria-label="审阅状态">
                  <option value="all">全部状态</option>{Object.entries(statusLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}
                </select>
              </div>
            </header>
            <div className="submissionQueue">
              {visibleSubmissions.map((item) => (
                <article className="reviewCard" key={item.id}>
                  <header>
                    <div><span className={`kindBadge ${item.kind}`}>{item.kind === "feature" ? "新功能" : "Bug"}</span><time>{new Date(item.created_at).toLocaleString("zh-CN")}</time>{item.kind === "feature" && <span aria-label={`点赞 ${item.like_count}`}>♥ {item.like_count}</span>}</div>
                    <strong>{item.title}</strong>
                  </header>
                  <p className="reviewBody">{item.body}</p>
                  <div className="reviewIdentity"><span>@{item.nickname}</span><a href={`mailto:${item.email}`}>{item.email}</a></div>
                  {item.attachments.length > 0 && <div className="reviewAttachments">{item.attachments.map((attachment) => attachment.signedUrl ? <a href={attachment.signedUrl} target="_blank" rel="noreferrer" key={attachment.path}>{attachment.type.startsWith("video/") ? "视频" : "图片"} · {attachment.name} <ExternalArrow /></a> : <span key={attachment.path}>{attachment.name}</span>)}</div>}
                  <div className="reviewControls">
                    <label><span>审阅状态</span><select value={item.status} onChange={(event) => editSubmission(item.id, { status: event.target.value as SubmissionStatus })}>{Object.entries(statusLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
                    <label><span>奖励</span><select value={item.reward_status} onChange={(event) => editSubmission(item.id, { reward_status: event.target.value as RewardStatus })}>{Object.entries(rewardLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
                    <label className="reviewNote"><span>内部备注</span><textarea value={item.reviewer_note ?? ""} onChange={(event) => editSubmission(item.id, { reviewer_note: event.target.value })} rows={3} /></label>
                    <button className="button buttonPrimary" onClick={() => saveSubmission(item)} disabled={savingId === item.id}>{savingId === item.id ? "保存中…" : "保存审阅"}</button>
                  </div>
                </article>
              ))}
              {!visibleSubmissions.length && <p className="adminEmpty">当前筛选条件下没有提交。</p>}
            </div>
          </>
        ) : (
          <>
            <header className="adminPageHeader"><div><p>RELEASE PREVIEW</p><h2>发布版本预告</h2></div></header>
            <form className="previewEditor" onSubmit={createPreview}>
              <div className="previewEditorMeta"><label><span>版本号</span><input name="version" placeholder="v2.1 Beta" required /></label><label><span>预计日期</span><input name="target_date" type="date" /></label><label><span>发布状态</span><select name="status"><option value="draft">先存草稿</option><option value="published">立即发布</option></select></label></div>
              <div className="previewEditorLanguages">
                <fieldset><legend>中文内容</legend><label><span>标题</span><input name="title_zh" required /></label><label><span>说明</span><textarea name="body_zh" rows={5} required /></label><label><span>亮点（每行一条）</span><textarea name="highlights_zh" rows={5} /></label></fieldset>
                <fieldset><legend>English</legend><label><span>Title</span><input name="title_en" /></label><label><span>Description</span><textarea name="body_en" rows={5} /></label><label><span>Highlights (one per line)</span><textarea name="highlights_en" rows={5} /></label></fieldset>
              </div>
              <button className="button buttonPrimary" type="submit">保存版本预告</button>
            </form>
            <div className="previewAdminList">
              {previews.map((preview) => <article key={preview.id}><div><span className={preview.status}>{preview.status === "published" ? "已发布" : "草稿"}</span><small>{preview.version}</small><h3>{preview.title_zh}</h3><p>{preview.body_zh}</p></div><button className="button buttonSecondary" onClick={() => togglePreview(preview)}>{preview.status === "published" ? "撤回为草稿" : "发布到前台"}</button></article>)}
              {!previews.length && <p className="adminEmpty">还没有版本预告。</p>}
            </div>
          </>
        )}
      </main>
    </div>
  );
}
