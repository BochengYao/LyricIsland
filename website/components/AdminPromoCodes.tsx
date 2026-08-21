"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { AdminNav } from "@/components/AdminNav";
import { LogoLockup } from "@/components/SitePage";
import type {
  DistributionStatus,
  PromoCode,
  PromoCodeLog,
  PromoCodeStats,
  TsvImportPreview,
  TsvImportResult,
  TsvParsedRow,
  AssignPromoCodeResult,
  PromoCodeOrder,
} from "@/data/promo-code-types";
import { parseTsvFile, generateImportPreview } from "@/lib/promo-code-tsv-parser";

/* ── Helpers ─────────────────────────────────────────────────────────────── */

type AuthState = "checking" | "login" | "ready";

const dateFormatter = new Intl.DateTimeFormat("zh-CN", {
  year: "numeric", month: "2-digit", day: "2-digit",
  hour: "2-digit", minute: "2-digit", second: "2-digit",
  hour12: false,
});

function formatDate(value: string | null): string {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "—";
  return dateFormatter.format(d);
}

function maskCode(code: string): string {
  if (code.length <= 8) return code;
  return code.slice(0, 5) + code.slice(5, -3).replace(/./g, "*") + code.slice(-3);
}

async function fetchWithTimeout(
  input: RequestInfo | URL,
  init: RequestInit,
  timeoutMs = 15_000,
) {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } finally {
    window.clearTimeout(timeout);
  }
}

async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

/* ── Constants ───────────────────────────────────────────────────────────── */

const statusLabels: Record<DistributionStatus, string> = {
  available: "可用",
  assigned: "已分配",
  revoked: "已撤销",
  expired: "已过期",
};

const statusBadgeClass: Record<DistributionStatus, string> = {
  available: "promoCodeBadge available",
  assigned: "promoCodeBadge assigned",
  revoked: "promoCodeBadge revoked",
  expired: "promoCodeBadge expired",
};

const msStatusLabels: Record<string, string> = {
  redeemed: "已兑换",
  available: "未兑换",
  unknown: "未知",
};

const channelOptions = ["官网", "QQ", "微信", "小红书", "GitHub", "活动", "补偿", "其他"] as const;

const PAGE_SIZE = 50;

/* ── Component ───────────────────────────────────────────────────────────── */

export function AdminPromoCodes() {
  /* Auth state */
  const [auth, setAuth] = useState<AuthState>("checking");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  /* Data state */
  const [codes, setCodes] = useState<PromoCode[]>([]);
  const [total, setTotal] = useState(0);
  const [stats, setStats] = useState<PromoCodeStats | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [orders, setOrders] = useState<PromoCodeOrder[]>([]);

  /* Batch operations */
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [batchSaving, setBatchSaving] = useState(false);

  /* Date range filter */
  const [dateField, setDateField] = useState<"imported_at" | "assigned_at" | "microsoft_expire_at" | "">("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");

  /* Filters */
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | DistributionStatus>("all");
  const [orderFilter, setOrderFilter] = useState("");
  const [channelFilter, setChannelFilter] = useState("");
  const searchTimer = useRef<ReturnType<typeof setTimeout>>(undefined);

  /* Modals */
  const [importOpen, setImportOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [drawerCode, setDrawerCode] = useState<PromoCode | null>(null);
  const [drawerLogs, setDrawerLogs] = useState<PromoCodeLog[]>([]);

  /* Import state */
  const [importPreview, setImportPreview] = useState<TsvImportPreview | null>(null);
  const [importRows, setImportRows] = useState<TsvParsedRow[]>([]);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<string | null>(null);

  /* Assign state */
  const [assignForm, setAssignForm] = useState({
    assigned_name: "",
    assigned_email: "",
    assigned_channel: "官网",
    campaign: "",
    note: "",
    specific_code_id: "",
  });
  const [assigning, setAssigning] = useState(false);
  const [assignResult, setAssignResult] = useState<AssignPromoCodeResult | null>(null);

  /* Toast */
  const [toast, setToast] = useState("");
  const toastTimer = useRef<ReturnType<typeof setTimeout>>(undefined);

  function showToast(message: string) {
    setToast(message);
    if (toastTimer.current) clearTimeout(toastTimer.current);
    toastTimer.current = setTimeout(() => setToast(""), 2500);
  }

  /* ── Auth ──────────────────────────────────────────────────────────────── */

  async function loadData(page = 1) {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(PAGE_SIZE));
    if (statusFilter !== "all") params.set("status", statusFilter);
    if (orderFilter) params.set("orderId", orderFilter);
    if (channelFilter) params.set("channel", channelFilter);
    if (search) params.set("search", search);
    if (dateField) params.set("dateField", dateField);
    if (dateFrom) params.set("dateFrom", dateFrom);
    if (dateTo) params.set("dateTo", dateTo);

    const response = await fetchWithTimeout(
      `/api/incentives/admin/promo-codes?${params}`,
      { cache: "no-store" },
    );
    if (response.status === 401) { setError(""); setAuth("login"); return; }
    if (!response.ok) {
      let body: { code?: string } | null = null;
      try { body = (await response.json()) as { code?: string }; } catch { /* non-JSON error body */ }
      if (response.status === 503 && body?.code === "TABLE_NOT_INITIALIZED") {
        throw new Error("促销代码数据表尚未初始化，请先在 Supabase 执行 supabase/schema.sql");
      }
      if (response.status >= 500) {
        throw new Error("促销代码数据服务异常，请稍后重试");
      }
      throw new Error("数据加载失败");
    }
    const data = (await response.json()) as {
      codes: PromoCode[];
      total: number;
      page: number;
      stats: PromoCodeStats;
      orders: PromoCodeOrder[];
    };
    setCodes(data.codes);
    setTotal(data.total);
    setStats(data.stats);
    setCurrentPage(data.page);
    if (data.orders) setOrders(data.orders);
    setError("");
    setAuth("ready");
  }

  useEffect(() => {
    loadData().catch((err: unknown) => {
      // 401 已由 loadData 内部处理（不会 throw 到这里）；
      // 数据层错误保持已认证状态，只展示错误横幅，不回弹登录页。
      setError(err instanceof Error ? err.message : "数据加载失败");
      setAuth((current) => (current === "checking" ? "ready" : current));
    });
  }, []);

  /* Reload on filter change */
  const prevFilters = useRef({ statusFilter, orderFilter, channelFilter, search, dateField, dateFrom, dateTo });
  useEffect(() => {
    const prev = prevFilters.current;
    const changed =
      prev.statusFilter !== statusFilter ||
      prev.orderFilter !== orderFilter ||
      prev.channelFilter !== channelFilter ||
      prev.search !== search ||
      prev.dateField !== dateField ||
      prev.dateFrom !== dateFrom ||
      prev.dateTo !== dateTo;
    if (changed && auth === "ready") {
      prevFilters.current = { statusFilter, orderFilter, channelFilter, search, dateField, dateFrom, dateTo };
      loadData(1).catch((err: unknown) => setError(err instanceof Error ? err.message : "数据加载失败"));
    }
  }, [statusFilter, orderFilter, channelFilter, search, dateField, dateFrom, dateTo, auth]);

  async function login(event: React.FormEvent) {
    event.preventDefault();
    setError("");
    const response = await fetch("/api/incentives/admin/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ password }),
    });
    const result = (await response.json()) as { error?: string };
    if (!response.ok) { setError(result.error ?? "登录失败"); return; }
    setPassword("");
    try {
      await loadData();
    } catch (err: unknown) {
      // 认证成功（cookie 已建立）但数据加载失败：转入 ready 态展示错误横幅 + 重试，
      // 而不是回到已清空密码的登录表单。
      setError(err instanceof Error ? err.message : "数据加载失败");
      setAuth("ready");
    }
  }

  async function logout() {
    await fetch("/api/incentives/admin/logout", { method: "POST" });
    setAuth("login");
  }

  /* ── Search debounce ──────────────────────────────────────────────────── */

  function handleSearchChange(value: string) {
    setSearch(value);
    if (searchTimer.current) clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => {
      setSearch(value);
    }, 300);
  }

  /* ── Pagination ───────────────────────────────────────────────────────── */

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const rangeStart = total === 0 ? 0 : (currentPage - 1) * PAGE_SIZE + 1;
  const rangeEnd = Math.min(currentPage * PAGE_SIZE, total);

  function goToPage(page: number) {
    const clamped = Math.max(1, Math.min(page, totalPages));
    loadData(clamped).catch((err: unknown) => setError(err instanceof Error ? err.message : "数据加载失败"));
  }

  /* ── Import ───────────────────────────────────────────────────────────── */

  async function handleFileSelect(file: File) {
    const parseResult = await parseTsvFile(file);
    setImportRows(parseResult.rows);

    // Call server for real preview (compares with DB)
    try {
      const response = await fetch("/api/incentives/admin/promo-codes/preview", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ rows: parseResult.rows })
      });
      if (response.ok) {
        const preview = await response.json();
        setImportPreview(preview);
      } else {
        // Fallback to client-side preview
        setImportPreview(generateImportPreview(file.name, parseResult));
      }
    } catch {
      // Fallback to client-side preview
      setImportPreview(generateImportPreview(file.name, parseResult));
    }
  }

  async function confirmImport() {
    if (!importPreview) return;
    setImporting(true);
    setImportResult(null);
    try {
      const response = await fetchWithTimeout("/api/incentives/admin/promo-codes", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ rows: importRows }),
      });
      if (response.status === 401) { setAuth("login"); return; }
      // ESA 返回扁平 { new_count, updated_count, ... }；SSR 过渡期可能返回嵌套 { result }。
      const body = (await response.json()) as Partial<TsvImportResult> & { result?: TsvImportResult; error?: string };
      if (!response.ok) throw new Error(body.error ?? "导入失败");
      const r = body.result ?? body;
      setImportResult(`成功导入 ${r.new_count ?? 0} 条新代码，更新 ${r.updated_count ?? 0} 条`);
      await loadData(currentPage);
    } catch (err: unknown) {
      setImportResult(err instanceof Error ? err.message : "导入失败");
    } finally {
      setImporting(false);
    }
  }

  function closeImportModal() {
    setImportOpen(false);
    setImportPreview(null);
    setImportRows([]);
    setImportResult(null);
  }

  /* ── Assign ───────────────────────────────────────────────────────────── */

  async function handleAssign(event: React.FormEvent) {
    event.preventDefault();
    setAssigning(true);
    setAssignResult(null);
    try {
      const body: Record<string, string> = {
        assigned_name: assignForm.assigned_name,
        assigned_email: assignForm.assigned_email,
        assigned_channel: assignForm.assigned_channel,
        campaign: assignForm.campaign,
      };
      if (assignForm.note) body.note = assignForm.note;
      if (assignForm.specific_code_id) body.specific_code_id = assignForm.specific_code_id;

      const response = await fetchWithTimeout("/api/incentives/admin/promo-codes/allocate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (response.status === 401) { setAuth("login"); return; }
      const result = (await response.json()) as AssignPromoCodeResult & { error?: string };
      if (!response.ok) throw new Error(result.error ?? "分配失败");
      setAssignResult(result);
      await loadData(currentPage);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "分配失败");
    } finally {
      setAssigning(false);
    }
  }

  function closeAssignModal() {
    setAssignOpen(false);
    setAssignResult(null);
    setAssignForm({ assigned_name: "", assigned_email: "", assigned_channel: "官网", campaign: "", note: "", specific_code_id: "" });
  }

  /* ── Detail drawer ────────────────────────────────────────────────────── */

  async function openDrawer(code: PromoCode) {
    setDrawerCode(code);
    setDrawerLogs([]);
    try {
      const response = await fetchWithTimeout(
        `/api/incentives/admin/promo-codes/${code.id}`,
        { cache: "no-store" },
      );
      if (response.status === 401) { setAuth("login"); return; }
      if (response.ok) {
        const data = (await response.json()) as { code: PromoCode; logs: PromoCodeLog[] };
        setDrawerCode(data.code);
        setDrawerLogs(data.logs ?? []);
      }
    } catch { /* ignore */ }
  }

  /* ── Export CSV ───────────────────────────────────────────────────────── */

  async function exportCsv() {
    const includeFullCode = window.confirm(
      "导出的文件可能包含可兑换的 Microsoft Store 促销代码。\n\n" +
      `点击\u201c确定\u201d导出包含完整兑换码的版本（请妥善保管）。\n` +
      `点击\u201c取消\u201d导出脱敏版本。`
    );

    try {
      const response = await fetch("/api/incentives/admin/promo-codes/export", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          filters: {
            status: statusFilter,
            orderId: orderFilter || undefined,
            channel: channelFilter || undefined,
            search: search || undefined,
          },
          includeFullCode
        })
      });

      if (response.status === 401) { setAuth("login"); return; }
      if (!response.ok) throw new Error("导出失败");

      const data = await response.json();
      const blob = new Blob([data.csv], { type: "text/csv;charset=utf-8" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `promo-codes-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setError(error instanceof Error ? error.message : "导出失败");
    }
  }

  /* ── Batch operations ─────────────────────────────────────────────────── */

  function toggleSelect(id: string) {
    setSelectedIds((prev) => prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]);
  }

  function toggleSelectAll() {
    if (selectedIds.length === codes.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(codes.map((c) => c.id));
    }
  }

  function clearSelection() {
    setSelectedIds([]);
  }

  async function batchEdit(field: string) {
    const value = window.prompt(`批量设置${field === "campaign" ? "Campaign" : field === "channel" ? "渠道" : "备注"}：`);
    if (value === null) return;
    setBatchSaving(true);
    try {
      const response = await fetch("/api/incentives/admin/promo-codes", {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ids: selectedIds, changes: { [field]: value } })
      });
      if (response.ok) {
        setSelectedIds([]);
        await loadData(currentPage);
      }
    } finally {
      setBatchSaving(false);
    }
  }

  /* ── MsStatus helper ──────────────────────────────────────────────────── */

  function getMsStatusLabel(code: PromoCode): string {
    if (code.microsoft_redeemed === true) return "redeemed";
    if (code.microsoft_redeemed === false) return "available";
    return "unknown";
  }

  function getMsBadgeClass(code: PromoCode): string {
    if (code.microsoft_redeemed === true) return "promoCodeBadge redeemed";
    if (code.microsoft_redeemed === false) return "promoCodeBadge available";
    return "promoCodeBadge unknown";
  }

  /* ── Render: Auth checking ────────────────────────────────────────────── */

  if (auth === "checking") {
    return (
      <div className="adminShell">
        <main className="adminMain" style={{ display: "grid", placeItems: "center", minHeight: "60vh" }}>
          <p style={{ color: "var(--orbit)" }}>正在验证登录状态…</p>
        </main>
      </div>
    );
  }

  /* ── Render: Login ────────────────────────────────────────────────────── */

  if (auth === "login") {
    return (
      <div className="adminShell">
        <main className="adminMain">
          <section className="adminLoginCard">
            <LogoLockup />
            <p className="eyebrow"><span aria-hidden="true">•</span>维护者后台</p>
            <h1>促销代码管理</h1>
            <p>登录后可以管理促销代码的导入、分配与查询。</p>
            <form onSubmit={login}>
              <label><span>后台密码</span><input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" required /></label>
              <button className="button buttonPrimary" type="submit">进入后台</button>
            </form>
            {error && <p className="adminError" role="alert">{error}</p>}
            <Link href="/incentives">← 返回用户激励计划</Link>
          </section>
        </main>
      </div>
    );
  }

  /* ── Render: Ready ────────────────────────────────────────────────────── */

  return (
    <div className="adminShell">
      <AdminNav
        active="promo-codes"
        onNavigate={(panel) => {
          if (panel === "feedback") window.location.href = "/admin";
        }}
        onLogout={logout}
      />

      <main className="adminMain">
        {error && (
          <div className="adminError" role="alert">
            <p>{error}</p>
            <button className="button buttonSecondary" onClick={() => loadData(currentPage).catch((err: unknown) => setError(err instanceof Error ? err.message : "数据加载失败"))}>重试</button>
          </div>
        )}

        {/* Stats cards */}
        {stats && (
          <div className="promoCodeStats">
            <div className="promoCodeStatCard">
              <p>总计</p>
              <strong>{stats.total}</strong>
            </div>
            <div className="promoCodeStatCard">
              <p>可用</p>
              <strong>{stats.available}</strong>
            </div>
            <div className="promoCodeStatCard">
              <p>已分配</p>
              <strong>{stats.assigned}</strong>
            </div>
            <div className="promoCodeStatCard">
              <p>已兑换</p>
              <strong>{stats.microsoft_redeemed}</strong>
            </div>
            <div className="promoCodeStatCard">
              <p>已过期</p>
              <strong>{stats.expired}</strong>
            </div>
            <div className="promoCodeStatCard warn">
              <p>即将到期</p>
              <strong>{stats.expiring_soon}</strong>
            </div>
          </div>
        )}

        {/* Toolbar */}
        <header className="adminPageHeader">
          <div>
            <p>PROMO CODE MANAGEMENT</p>
            <h2>促销代码管理</h2>
          </div>
          <div className="adminToolbar">
            <button className="button buttonPrimary" onClick={() => setImportOpen(true)}>导入 TSV</button>
            <button className="button buttonSecondary" onClick={() => setAssignOpen(true)}>分配代码</button>
            <button className="button buttonSecondary" onClick={exportCsv}>导出 CSV</button>
          </div>
        </header>

        {/* Filters */}
        <div className="promoCodeFilters">
          <input
            type="search"
            placeholder="搜索代码、用户、订单…"
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
          />
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as "all" | DistributionStatus)}>
            <option value="all">全部状态</option>
            {Object.entries(statusLabels).map(([value, label]) => (
              <option value={value} key={value}>{label}</option>
            ))}
          </select>
          <select value={orderFilter} onChange={(e) => setOrderFilter(e.target.value)}>
            <option value="">全部订单</option>
            {orders.map((o) => (
              <option value={o.id} key={o.id}>{o.order_name ?? o.id}</option>
            ))}
          </select>
          <select value={channelFilter} onChange={(e) => setChannelFilter(e.target.value)}>
            <option value="">全部渠道</option>
            {channelOptions.map((ch) => (
              <option value={ch} key={ch}>{ch}</option>
            ))}
          </select>
          <select value={dateField} onChange={(e) => setDateField(e.target.value as typeof dateField)}>
            <option value="">日期字段</option>
            <option value="imported_at">导入时间</option>
            <option value="assigned_at">分配时间</option>
            <option value="microsoft_expire_at">到期时间</option>
          </select>
          {dateField && (
            <>
              <input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} placeholder="开始日期" />
              <input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} placeholder="结束日期" />
            </>
          )}
        </div>

        {/* Batch action bar */}
        {selectedIds.length > 0 && (
          <div className="promoCodeBatchBar">
            <span>已选择 {selectedIds.length} 条</span>
            <button onClick={() => batchEdit("campaign")} disabled={batchSaving}>批量设置 Campaign</button>
            <button onClick={() => batchEdit("channel")} disabled={batchSaving}>批量设置渠道</button>
            <button onClick={() => batchEdit("note")} disabled={batchSaving}>批量添加备注</button>
            <button onClick={clearSelection} disabled={batchSaving}>取消选择</button>
          </div>
        )}

        {/* Table */}
        <div className="adminTableWrap">
          <table className="adminDataTable">
            <thead>
              <tr>
                <th style={{ width: 40 }}>
                  <input type="checkbox" checked={codes.length > 0 && selectedIds.length === codes.length} onChange={toggleSelectAll} />
                </th>
                <th>Code</th>
                <th>内部状态</th>
                <th>Microsoft 状态</th>
                <th>Order</th>
                <th>Campaign</th>
                <th>发放对象</th>
                <th>渠道</th>
                <th>到期时间</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {codes.map((code) => (
                <tr key={code.id}>
                  <td>
                    <input type="checkbox" checked={selectedIds.includes(code.id)} onChange={() => toggleSelect(code.id)} />
                  </td>
                  <td><code className="promoCodeMono">{maskCode(code.code)}</code></td>
                  <td><span className={statusBadgeClass[code.distribution_status]}>{statusLabels[code.distribution_status]}</span></td>
                  <td><span className={getMsBadgeClass(code)}>{msStatusLabels[getMsStatusLabel(code)]}</span></td>
                  <td>{code.order_id ? (codes.find((c) => c.order_id === code.order_id) ? code.raw_order_id ?? code.order_id : code.raw_order_id ?? "—") : code.raw_order_id ?? "—"}</td>
                  <td>{code.campaign ?? "—"}</td>
                  <td>{code.assigned_to_name ?? "—"}</td>
                  <td>{code.assigned_channel ?? "—"}</td>
                  <td>{formatDate(code.microsoft_expire_at)}</td>
                  <td className="promoCodeActions">
                    <button className="button buttonSecondary" onClick={() => openDrawer(code)}>查看</button>
                    <button className="button buttonSecondary" onClick={async () => {
                      if (await copyToClipboard(code.code)) showToast("兑换码已复制");
                    }}>复制</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {codes.length === 0 && <p className="adminEmpty">当前筛选条件下没有促销代码。</p>}
        </div>

        {/* Pagination */}
        {total > PAGE_SIZE && (
          <div className="promoCodePagination">
            <span>显示 {rangeStart}–{rangeEnd} / 共 {total} 条</span>
            <div>
              <button className="button buttonSecondary" disabled={currentPage <= 1} onClick={() => goToPage(currentPage - 1)}>上一页</button>
              <span className="promoCodePageInfo">{currentPage} / {totalPages}</span>
              <button className="button buttonSecondary" disabled={currentPage >= totalPages} onClick={() => goToPage(currentPage + 1)}>下一页</button>
            </div>
          </div>
        )}
      </main>

      {/* Import Modal */}
      {importOpen && (
        <div className="adminModalBackdrop" role="presentation" onMouseDown={(e) => { if (e.target === e.currentTarget) closeImportModal(); }}>
          <section className="promoCodeImportModal" role="dialog" aria-modal="true" aria-labelledby="import-modal-title">
            <header>
              <div><p>IMPORT TSV</p><h2 id="import-modal-title">导入促销代码</h2></div>
              <button aria-label="关闭" onClick={closeImportModal}>×</button>
            </header>

            {!importPreview ? (
              <div className="promoCodeDropZone"
                onDragOver={(e) => { e.preventDefault(); e.stopPropagation(); }}
                onDrop={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  const file = e.dataTransfer.files[0];
                  if (file) void handleFileSelect(file);
                }}
              >
                <p>拖拽 TSV 文件到此处，或</p>
                <label className="button buttonSecondary">
                  选择文件
                  <input type="file" accept=".tsv,.txt" hidden onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) void handleFileSelect(file);
                    e.target.value = "";
                  }} />
                </label>
              </div>
            ) : importResult ? (
              <div className="promoCodeImportResult">
                <p>{importResult}</p>
                <button className="button buttonPrimary" onClick={closeImportModal}>完成</button>
              </div>
            ) : (
              <>
                <div className="promoCodeImportSummary">
                  <p>检测到 <strong>{importPreview.total_detected}</strong> 条代码</p>
                  {importPreview.errors.length > 0 && (
                    <p className="adminError">{importPreview.errors.length} 条解析错误</p>
                  )}
                  {importPreview.warnings.length > 0 && (
                    <p style={{ color: "#b8860b" }}>{importPreview.warnings.length} 条警告</p>
                  )}
                </div>
                <footer>
                  <button className="button buttonSecondary" onClick={() => { setImportPreview(null); setImportRows([]); }}>重新选择</button>
                  <button className="button buttonPrimary" disabled={importing || importRows.length === 0} onClick={confirmImport}>
                    {importing ? "导入中…" : "确认导入"}
                  </button>
                </footer>
              </>
            )}
          </section>
        </div>
      )}

      {/* Assign Modal */}
      {assignOpen && (
        <div className="adminModalBackdrop" role="presentation" onMouseDown={(e) => { if (e.target === e.currentTarget) closeAssignModal(); }}>
          <section className="promoCodeAssignModal" role="dialog" aria-modal="true" aria-labelledby="assign-modal-title">
            <header>
              <div><p>ASSIGN CODE</p><h2 id="assign-modal-title">分配促销代码</h2></div>
              <button aria-label="关闭" onClick={closeAssignModal}>×</button>
            </header>

            {assignResult ? (
              <div className="promoCodeAssignResult">
                <p>分配成功！</p>
                <div className="promoCodeResultRow">
                  <label>兑换码</label>
                  <code>{assignResult.code}</code>
                  <button className="button buttonSecondary" onClick={async () => {
                    if (await copyToClipboard(assignResult.code)) showToast("兑换码已复制");
                  }}>复制代码</button>
                </div>
                {assignResult.redeem_url && (
                  <div className="promoCodeResultRow">
                    <label>兑换链接</label>
                    <input type="text" readOnly value={assignResult.redeem_url} />
                    <button className="button buttonSecondary" onClick={async () => {
                      if (await copyToClipboard(assignResult.redeem_url!)) showToast("链接已复制");
                    }}>复制链接</button>
                  </div>
                )}
                <footer>
                  <button className="button buttonPrimary" onClick={closeAssignModal}>完成</button>
                </footer>
              </div>
            ) : (
              <form onSubmit={handleAssign}>
                <div className="editFormGrid">
                  <label><span>用户标识</span><input value={assignForm.specific_code_id} onChange={(e) => setAssignForm({ ...assignForm, specific_code_id: e.target.value })} placeholder="留空则自动分配" /></label>
                  <label><span>姓名</span><input value={assignForm.assigned_name} onChange={(e) => setAssignForm({ ...assignForm, assigned_name: e.target.value })} required /></label>
                  <label><span>Email</span><input type="email" value={assignForm.assigned_email} onChange={(e) => setAssignForm({ ...assignForm, assigned_email: e.target.value })} required /></label>
                  <label><span>渠道</span>
                    <select value={assignForm.assigned_channel} onChange={(e) => setAssignForm({ ...assignForm, assigned_channel: e.target.value })}>
                      {channelOptions.map((ch) => <option value={ch} key={ch}>{ch}</option>)}
                    </select>
                  </label>
                  <label><span>Campaign</span><input value={assignForm.campaign} onChange={(e) => setAssignForm({ ...assignForm, campaign: e.target.value })} /></label>
                  <label className="wide"><span>备注</span><textarea rows={3} value={assignForm.note} onChange={(e) => setAssignForm({ ...assignForm, note: e.target.value })} /></label>
                </div>
                <footer>
                  <button className="button buttonSecondary" type="button" onClick={closeAssignModal}>取消</button>
                  <button className="button buttonPrimary" type="submit" disabled={assigning}>
                    {assigning ? "分配中…" : "确认分配"}
                  </button>
                </footer>
              </form>
            )}
          </section>
        </div>
      )}

      {/* Detail Drawer */}
      {drawerCode && (
        <div className="adminModalBackdrop" role="presentation" onMouseDown={(e) => { if (e.target === e.currentTarget) setDrawerCode(null); }}>
          <aside className="promoCodeDrawer" role="dialog" aria-modal="true" aria-labelledby="drawer-title">
            <header>
              <div><p>PROMO CODE DETAIL</p><h2 id="drawer-title">代码详情</h2></div>
              <button aria-label="关闭" onClick={() => setDrawerCode(null)}>×</button>
            </header>

            <div className="promoCodeDrawerBody">
              <section className="promoCodeDrawerSection">
                <h3>Microsoft Store</h3>
                <dl>
                  <dt>Code ID</dt><dd><code>{drawerCode.microsoft_code_id}</code></dd>
                  <dt>Promo Code</dt><dd><code>{drawerCode.code}</code> <button className="button buttonSecondary" style={{ marginLeft: 8 }} onClick={async () => { if (await copyToClipboard(drawerCode.code)) showToast("兑换码已复制"); }}>复制</button></dd>
                  <dt>兑换链接</dt><dd>{drawerCode.redeem_url ? <a href={drawerCode.redeem_url} target="_blank" rel="noopener noreferrer">{drawerCode.redeem_url}</a> : "—"}</dd>
                  <dt>Order</dt><dd>{drawerCode.raw_order_id ?? "—"}</dd>
                  <dt>可用</dt><dd>{drawerCode.microsoft_available === true ? "是" : drawerCode.microsoft_available === false ? "否" : "—"}</dd>
                  <dt>已兑换</dt><dd>{drawerCode.microsoft_redeemed === true ? "是" : drawerCode.microsoft_redeemed === false ? "否" : "—"}</dd>
                  <dt>生效时间</dt><dd>{formatDate(drawerCode.microsoft_start_at)}</dd>
                  <dt>到期时间</dt><dd>{formatDate(drawerCode.microsoft_expire_at)}</dd>
                  <dt>最后同步</dt><dd>{formatDate(drawerCode.microsoft_synced_at)}</dd>
                </dl>
              </section>

              <section className="promoCodeDrawerSection">
                <h3>歌词岛</h3>
                <dl>
                  <dt>状态</dt><dd><span className={statusBadgeClass[drawerCode.distribution_status]}>{statusLabels[drawerCode.distribution_status]}</span></dd>
                  <dt>分配用户</dt><dd>{drawerCode.assigned_to_name ?? "—"}</dd>
                  <dt>邮箱</dt><dd>{drawerCode.assigned_to_email ?? "—"}</dd>
                  <dt>渠道</dt><dd>{drawerCode.assigned_channel ?? "—"}</dd>
                  <dt>Campaign</dt><dd>{drawerCode.campaign ?? "—"}</dd>
                  <dt>分配时间</dt><dd>{formatDate(drawerCode.assigned_at)}</dd>
                  <dt>备注</dt><dd>{drawerCode.note ?? "—"}</dd>
                </dl>
              </section>

              <section className="promoCodeDrawerSection">
                <h3>审计日志</h3>
                {drawerLogs.length === 0 ? (
                  <p className="adminEmpty">暂无审计记录。</p>
                ) : (
                  <ol className="promoCodeTimeline">
                    {drawerLogs.map((log) => (
                      <li key={log.id}>
                        <time>{formatDate(log.created_at)}</time>
                        <strong>{log.action}</strong>
                        {log.operator_email && <span>{log.operator_email}</span>}
                      </li>
                    ))}
                  </ol>
                )}
              </section>
            </div>
          </aside>
        </div>
      )}

      {/* Toast */}
      {toast && <div className="promoCodeToast">{toast}</div>}
    </div>
  );
}
