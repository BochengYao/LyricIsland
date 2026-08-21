# 任务交接：新功能页全局亮点与详情页版本选择布局

- 日期：2026-08-13
- 任务线程：01-web-frontend
- 基线提交：`origin/main` `652f55779bbf860bd7b4ec450bda248f2791bf51`
- 结果提交：待本分支提交
- 允许修改范围：公开官网更新页组件与样式；不修改后台、API、鉴权、数据库、内容数据、根工作树或发布环境。

## 已完成

- 概览页只保留既有英雄区和唯一一个全局 `content.summary` 更新亮点；亮点不再与版本选择或模块列表耦合。
- 版本选择器移至版本详情页的标题行，与橙点/当前完整版本标签同一行；选择器仅控制下面的完整模块列表。
- 移除了 `updatesVersionPicker` 的整宽 `surface` 背景、圆角和内边距，仅保留标签与下拉框本身的小胶囊；窄屏下标题行和控件自然纵向排列。
- 继续保留所选版本全部模块的编号、标题、描述、完整小点，以及既有四语 hero、导航、下载收尾与页脚。

## 未修改 / 非目标

- 未改 `updates-copy.ts`、更新亮点内容、版本内容或任何公开数据读取契约。
- 未改 `website/app/api/`、管理后台、鉴权、Supabase 或发布环境；未部署 ESA。

## 验证

- 命令：`npm run typecheck`
- 结果：通过。
- 命令：`npm run build:esa`
- 结果：通过；生成 16 条静态路由，包含 `/updates`、`/en/updates`、`/zh-hant/updates`、`/ja/updates`。
- 局部 DOM/样式验收：使用本地 `next start` 和 Playwright/Edge，以两版模拟公开响应验证四语路由。每个 overview 只有 1 个 `.updatesSummary` 且没有 `.updatesVersionPicker`；每个 details 区有 1 个 `.releaseHeader` 与 1 个选择器；选择从 `V2.0.36` 到 `V2.0.35` 时模块数由 9 变为 1，而 overview 亮点文本不变。选择器外层计算样式为透明背景、`0px` 内边距。
- 未运行：`npm run test:esa-api`，因为本次未修改前台读取契约或 API。

## 风险与后续

- 已知限制：本地 DOM 验收使用模拟 `/api/features` 响应；发布后仍应以真实公开数据确认视觉排版。
- 交接目标：项目统筹 / 发布线程；ESA 同步后按四语路由人工复核。
- 回滚点：`652f55779bbf860bd7b4ec450bda248f2791bf51`。
