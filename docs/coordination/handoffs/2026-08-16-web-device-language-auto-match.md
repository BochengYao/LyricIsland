# 任务交接：官网未带语言入口按设备语言自动匹配

- 日期：2026-08-16
- 任务线程：官网前台
- 基线提交：`e9d4d65a2e111092e7cdfc83ff93c48063498303`
- 结果提交：待本分支提交
- 允许修改范围：`website/app/(zh)/` 的公开入口、公开组件与公开样式。

## 已完成

- 核对当前 main：四语公开路由已存在，覆盖首页、`/updates` 与 `/incentives`；未修改路由结构、后台或 API。
- 无前缀公开入口 `/`、`/updates`、`/incentives` 注入静态导出兼容的 `beforeInteractive` 客户端脚本：优先读取保存的用户语言，否则依次匹配 `navigator.languages`（为空时回退 `navigator.language`）。繁中匹配 `zh-Hant`、`zh-TW`、`zh-HK`、`zh-MO`；`ja*`、`en*`、其他 `zh*` 及未知语言分别映射日文、英文、简中、英文。
- 仅无前缀入口会执行该脚本；显式 `/en`、`/zh-hant`、`/ja` 不会被自动覆盖。跳转使用 `location.replace`，保留 pathname、query 与 hash，并在目标与当前地址相同时不跳转。
- 语言菜单的手动语言选择保存到 `localStorage`；新增四语“跟随设备语言”菜单项，清除保存项并跳转到当前设备语言。键盘方向键、Home/End/Escape 导航覆盖新菜单项。

## 未修改 / 非目标

- 未修改管理后台、`website/app/api/`、鉴权、数据库、接口契约或内容数据。
- 未修改显式语言页、SEO canonical/hreflang 的既有定义；静态页仍可被抓取。
- 未触发 ESA 部署。

## 验证

- 命令：`npm run typecheck`
- 结果：通过（退出码 0）。
- 命令：`npm run build:esa`
- 结果：通过（退出码 0）；静态导出包含四语首页、更新页、激励页共 12 个公开路由文件。
- 单元层：直接执行 `resolveBrowserLocale` 矩阵。`zh-CN → zh`，`zh-TW`/`zh-HK`/`zh-MO`/`zh-Hant → zhHant`，`ja-JP → ja`，`en-US → en`，`fr-FR → en`，且 `navigator.languages` 为空时 `navigator.language=ja` 回退到 `ja`。
- 静态产物检查：无前缀三个入口的导出 HTML 均包含 `unprefixed-locale-redirect`、`navigator.languages`、本地偏好键与 `/zh-hant` 映射；四语全部 12 条公开页面路径均存在。
- 浏览器矩阵：当前 Codex 环境中，按技能提供的独立 Python 静态服务启动后，Playwright/Edge 访问本机端口持续报 `net::ERR_EMPTY_RESPONSE`，而同进程 `curl` 返回 200；已按统筹要求停止端口排查。质量线程须在独立环境补做 `zh-CN`、`zh-TW`、`zh-HK`、`ja-JP`、`en-US`、未知语言、显式 `/en`、手动保存与“跟随设备语言”恢复场景，并确认 query/hash 保留及无循环。
- `npm run test:esa-api`：未运行；本任务未修改 API、数据读取或接口契约。

## 风险与后续

- 已知限制：本工作环境未完成端到端浏览器矩阵，不能以本地构建代替浏览器运行时验收。
- 交接目标：`05-quality-integration` 复测浏览器矩阵；通过后再交由 `06-release-github-store` 进行 ESA 发布与生产路由确认。
- 回滚点：本任务结果提交；移除无前缀入口的 `UnprefixedLocaleRedirect` 与语言菜单偏好逻辑即可恢复原行为。
