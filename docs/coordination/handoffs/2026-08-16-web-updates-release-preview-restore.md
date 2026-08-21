# 任务交接：恢复新功能页的版本预告

- 日期：2026-08-16
- 任务线程：官网前台
- 基线提交：`a023a6e3f536efc139d3b1db5cae47cb58cdfd43`
- 结果提交：待本分支提交
- 允许修改范围：`website/components/UpdatesPage.tsx`、`website/components/VersionPreviewSection.tsx` 与本交接记录。

## 已完成

- 审计确认根因不是 CSS 隐藏、路由或公开接口失效：`7648dc2a7bddc40c51093a0e2482cbf7f4b7f22c`（`feat(website): display feature release versions`，2026-08-13）在接入新功能版本选择器时，从 `UpdatesPage` 移除了 `VersionPreviewSection`、`DatabasePreload("/api/incentives/public")` 和该组件渲染。`VersionPreviewSection` 与 `.updatesPreviewSection` 历史双栏样式仍保留在当前 main。
- 恢复四语 `/updates` 的预告段：在既有总更新亮点、版本选择与模块详情之后，重新预加载并仅通过公开 `/api/incentives/public` 显示版本号、预计发布时间和逐条功能；不复制或固化预告数据。
- 保留原 `VersionPreviewSection` 的卡片、编号和响应式样式，视觉信息与历史参考一致。
- 修复该公开组件的四语读取：繁中优先 `_zh_tw` 再回退简中；日文优先 `_ja`、英文、简中；英文优先 `_en` 再回退简中。空数据、加载与接口失败提示现均有四语文本。

## 未修改 / 非目标

- 未修改后台、`website/app/api/`、鉴权、数据库、版本预告数据、数据模型或 API 契约。
- 未修改既有新功能版本选择器、全局更新亮点、模块化更新内容、路由或 SEO/hreflang。
- 未触发 ESA 部署；用户已授权的同步仍须由发布线程执行。

## 验证

- 命令：`npm run typecheck`
- 结果：通过（退出码 0）。
- 命令：`npm run test:esa-api`
- 结果：通过（`ESA API tests passed`）。
- 命令：`npm run build:esa`
- 结果：通过（退出码 0）；静态导出包含简中、英文、繁中和日文的首页、更新页、激励页路由。
- 浏览器：使用本机静态 ESA 产物和模拟的公开只读 `/api/features`、`/api/incentives/public` 响应，验收 `/updates/`、`/en/updates/`、`/zh-hant/updates/`、`/ja/updates/`。每页均显示 1 张版本预告卡、版本号、预计发布时间和 3 条功能；分别断言简中、英文、繁中、日文字段。空预告和接口 500 均显示正确的简中状态，未影响页面结构。

## 风险与后续

- 已知限制：浏览器验证使用公开接口的模拟响应，真实已发布预告内容与生产 ESA 仍须由质量/发布线程复核。
- 交接目标：`05-quality-integration` 复测公开四语 `/updates` 与真实公开 API；通过后由 `06-release-github-store` 合并、同步 ESA，并核验生产页面。
- 回滚点：`a023a6e3f536efc139d3b1db5cae47cb58cdfd43`；撤销本候选即可回到仅显示新功能内容的状态。
