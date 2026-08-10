# 任务交接：官网繁中与日文公开版本

- 日期：2026-08-10
- 任务线程：官网前台
- 基线提交：`cd93a62780848670a0ead5dbd77acf6fc0366d87`（`codex/feature/web-locales`，从 `main` 创建）
- 结果提交：待创建；通过本地官网发布前验证，待推送 `main` 触发 ESA 同步。
- 允许修改范围：公开路由、公开组件、公开文案、SEO 元数据和 `website/public/sitemap.xml`；在 `website/app/globals.css` 仅新增语言选择器专用样式，以维持现有 Mastercard 设计系统。

## 已完成

- 已复核官网前台章程、既定决策、官网接口约定、回归基线和完整 Mastercard DESIGN.md。
- 已建立独立官网工作树，不混入当前桌面本地化线程。
- 已新增繁中 `/zh-hant/` 与日文 `/ja/` 的首页、更新页和激励计划页，并在既有中英公开页中补齐四语言 hreflang。
- 已将导航栏的单一语言链接替换为四选项原生下拉框；桌面端直接显示，移动端位于同一导航菜单中，保留 Mastercard 风格的白色胶囊、暖黑描边、克制阴影与 44 px 触控高度。
- 已以桌面端术语对齐“繁體中文”“日本語”、`LyricHover`、播放器、Pro 与 Microsoft Store 表述；繁中首页使用繁体字，日文首页保留短句节奏与对应的对仗表达。
- 已同步结构化数据、每个公开页的独立 canonical/hreflang 与全量互链 sitemap。

## 未修改 / 非目标

- 不修改 `website/app/api/`、管理后台、`website/lib/`、数据库、服务端鉴权或桌面应用代码。
- 不改变公开接口的数据结构、提交、点赞、预加载或鉴权行为。

## 验证

- 命令：在 `website/` 运行 `npm run typecheck`、`npm run build:esa`、`npm run test:esa-api`。
- 结果：三项均通过（退出码 0）；ESA 构建生成 16 条静态路由，包含 `/zh-hant`、`/zh-hant/updates`、`/zh-hant/incentives`、`/ja`、`/ja/updates`、`/ja/incentives`。
- 手动：以构建产物运行浏览器回归，确认 `/`、`/zh-hant/`、`/en/`、`/ja/`、`/zh-hant/updates/`、`/ja/incentives/` 的 `lang`、四选项语言选择器与标题；确认桌面端切换至日文、移动端繁中导航菜单内的选择器可见。

## 风险与后续

- 已知限制：公开激励内容、版本预告与更新详情由既有只读接口提供，且当前只有中英字段；不增加字段或接口时，繁中回退中文、日文回退英文。这是有意维持 API 契约的展示回退。
- 发布目标：ESA 生产环境；推送后复核新增路由、SEO 与 sitemap 的实际响应。
- 回滚点：`cd93a62780848670a0ead5dbd77acf6fc0366d87`。
