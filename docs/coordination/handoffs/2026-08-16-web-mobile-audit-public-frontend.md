# 任务交接：公开官网移动端全量审计与触控修复

- 日期：2026-08-16
- 任务线程：`01-web-frontend`
- 基线提交：`3b0a51b8e5c236398e3faa3c5fb3806996ee5a29`
- 结果提交：待本分支提交
- 允许修改范围：公开前台 `website/app/globals.css` 与本交接记录；不修改后台、`website/app/api/`、数据、鉴权或 ESA 部署。

## 已完成

- 在隔离、无登录的 Chrome CDP 静态服务中审计四语公开页：简中 `/`、`/updates`、`/incentives`；英文 `/en`、`/en/updates`、`/en/incentives`；繁中 `/zh-hant`、`/zh-hant/updates`、`/zh-hant/incentives`；日文 `/ja`、`/ja/updates`、`/ja/incentives`。每页覆盖 `320×568`、`360×800`、`390×844`、`414×896` 竖屏，合计 48 个页面状态和 12 个 390px 移动菜单状态；触控问题额外检查 `844×390` 横屏（12 页及 12 个菜单状态）。
- 所有截图已保存并以图像解码方式逐张打开验证：初始 60 张、最终 61 张、横屏 24 张、键盘焦点 3 张，均可用。
- 初始发现并修复三类公开移动端触控目标：
  - 首页演示状态切换的“播放/空闲”按钮高度为 32px；移动端改为最小 44px。
  - 首页页脚产品/资源链接高度为 32px；移动端改为最小 44px。
  - 更新页和激励页页脚“返回官网”仅有文字高度；移动端改为 44px 的 `inline-flex` 触控区域。
- 初始全页截图中“模块化布局”出现近乎纯黑块，经实际滚动触发组件既有滚动进度后，模块岛内容完整可见；此为未滚动全页截图未进入滚动阶段的表现，**不是产品问题，未为此修改代码**。最终滚动截图已确认内容可见。
- 所有 48 个竖屏页面均无文档级横向滚动或浏览器运行时异常；四语长标题、版本选择器、更新模块/预告、激励表单与奖励卡均可见。移动菜单按钮为 `48×48`，菜单在四语下可见；键盘 Tab 实测跳过链接显示实线焦点轮廓。

## 未修改 / 非目标

- 未改管理后台/API/鉴权/数据库/数据模型/预告或功能内容，也未部署 ESA。
- 未运行 `npm run test:esa-api`：本提交只改移动端公开 CSS，不涉及 API 或读取契约。
- 原 `web-mobile-audit` 树曾混入后台线程的未提交 `globals.css` 管理端 hunk；为避免覆盖或提交他人 WIP，本候选在独立干净树 `web-mobile-audit-frontend` 创建。

## 验证

- 命令：`npm run typecheck`
  - 结果：通过（退出码 0）。
- 命令：`npm run build:esa`
  - 结果：通过（退出码 0）；简中、英文、繁中、日文的首页、更新页、激励页静态路由均生成。
- 命令：隔离 Node 静态服务 + Chrome CDP；只模拟公开 `GET /api/features` 与 `GET /api/incentives/public` 成功响应。
  - 结果：四语 48 个竖屏页面 `horizontalOverflow=0`、`exceptions=[]`；最终矩阵输出 `screenshots: 60`。横屏矩阵同样 `horizontalOverflow=0`、`exceptions=[]`，输出 `screenshots: 24`。小尺寸输入仅为视觉隐藏、由外层 48px 文件选择按钮承载的 `input[type=file]`，不作为触控缺陷。
- 证据：
  - 初始：`C:\Users\14731\.codex\visualizations\2026\08\06\019fd7a2-bdda-7972-8c1b-28dde37aa41b\mobile-audit-before\`
  - 最终：`C:\Users\14731\.codex\visualizations\2026\08\06\019fd7a2-bdda-7972-8c1b-28dde37aa41b\mobile-audit-final\`
  - 横屏：`C:\Users\14731\.codex\visualizations\2026\08\06\019fd7a2-bdda-7972-8c1b-28dde37aa41b\mobile-audit-landscape\`
  - 键盘焦点与滚动模块：`C:\Users\14731\.codex\visualizations\2026\08\06\019fd7a2-bdda-7972-8c1b-28dde37aa41b\mobile-audit-focus-check\`、`C:\Users\14731\.codex\visualizations\2026\08\06\019fd7a2-bdda-7972-8c1b-28dde37aa41b\mobile-audit-final\zh-home-320x568-module-scrolled.png`

## 风险与后续

- 已知限制：浏览器矩阵用公开接口模拟数据，不能替代 ESA/CDN 生产响应和真实内容长度复核；正式上线后应由质量/发布线程再检查四语生产路径、首屏与长内容。
- 交接目标：`05-quality-integration` 复核移动端公开页面；通过后交 `06-release-github-store`。ESA 只由发布线程触发。
- 回滚点：`3b0a51b8e5c236398e3faa3c5fb3806996ee5a29`；移除本提交三条移动端公开选择器即可恢复。
