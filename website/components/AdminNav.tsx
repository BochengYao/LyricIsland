"use client";

import Link from "next/link";
import { LogoLockup } from "@/components/SitePage";
import { ExternalArrow } from "@/components/ExternalArrow";

type AdminNavProps = {
  active: "feedback" | "promo-codes";
  /** Which feedback sub-panel is active (only used when active="feedback") */
  activePanel?: string;
  pendingCount?: number;
  unreadAlerts?: number;
  onNavigate?: (panel: string) => void;
  onLogout?: () => void;
};

export function AdminNav({ active, activePanel = "submissions", pendingCount, unreadAlerts, onNavigate, onLogout }: AdminNavProps) {
  return (
    <aside className="adminSidebar">
      <LogoLockup />
      <p className="eyebrow"><span aria-hidden="true">•</span>维护者后台</p>
      <h1>{active === "feedback" ? "用户反馈" : "促销代码"}</h1>
      <nav aria-label="后台导航">
        <button className={active === "feedback" ? "isActive" : ""} onClick={() => onNavigate?.("submissions")}>反馈管理 {pendingCount != null && pendingCount > 0 && <span>{pendingCount}</span>}</button>
        <button className={active === "feedback" ? "isActive" : ""} onClick={() => onNavigate?.("features")}>新功能内容</button>
        <button className={active === "feedback" ? "isActive" : ""} onClick={() => onNavigate?.("previews")}>版本预告</button>
        <button className={active === "feedback" ? "isActive" : ""} onClick={() => onNavigate?.("access")}>访问日志 {unreadAlerts != null && unreadAlerts > 0 && <span className="alertCount">{unreadAlerts}</span>}</button>
        <button className={active === "promo-codes" ? "isActive" : ""} onClick={() => onNavigate?.("promo-codes")}>促销代码</button>
      </nav>
      <div className="adminSidebarBottom">
        <Link href="/incentives" target="_blank">查看前台 <ExternalArrow /></Link>
        <Link href="/updates" target="_blank">查看新功能页 <ExternalArrow /></Link>
        {onLogout && <button onClick={onLogout}>退出登录</button>}
      </div>
    </aside>
  );
}
