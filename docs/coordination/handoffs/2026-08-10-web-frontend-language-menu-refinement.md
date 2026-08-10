# 任务交接：官网语言菜单视觉修正

- 日期：2026-08-10
- 任务线程：官网前台
- 基线提交：`644a8945f0940e935a831ebe6bb41f588f4f1d70`
- 结果提交：`c753ab52f3011944a04195421d82f027295021d3`；已推送 `main` 并完成 ESA 生产同步。
- 允许修改范围：公开导航组件及其专用样式。

## 已完成

- 用自定义、可访问的语言菜单替换浏览器原生 `<select>` 弹层，消除灰色直角选项面板与蓝色系统焦点环。
- 触发器与菜单采用官网既有的白色全圆角胶囊、暖黑细描边、米色当前态和柔和悬浮阴影；选项高度不低于 44 px。
- 支持鼠标外部点击关闭、Enter/Space 点击、上下方向键、Home/End、Escape 关闭并将焦点归还触发器；当前语言以 `aria-current` 标注。
- 移动端菜单仍收纳在原有导航面板中。

## 未修改 / 非目标

- 未修改管理后台、`website/app/api/`、数据库、鉴权、公开接口或 SEO 路由。
- 未修改发布以外的基础设施配置。

## 验证

- 命令：在 `website/` 运行 `npm run typecheck`、`npm run build:esa`、`npm run test:esa-api`。
- 结果：三项均通过（退出码 0）；ESA 构建包含 16 个静态路由。
- 浏览器：已准备桌面和移动端的键盘、Escape、当前项和响应式断言；本机 Playwright 无预装 headless Chromium，改用本机 Chrome 后本地测试服务端口未返回 HTTP 响应，未将此环境问题视为组件通过。发布前应再次执行该手动视觉检查。
- 生产：`https://lyric-island.top/` 返回 ESA 200，页面包含 `languageMenuTrigger` 与 `aria-haspopup="menu"`，且不再输出原生 `<select>` 或旧 `languageSelect` 样式类。

## 风险与后续

- 已知限制：本次未获得可用的本地浏览器实机截图；生产 HTML 已确认移除原生 `select`，但建议后续有可用浏览器时补充桌面、移动端的视觉截图。
- 发布状态：官网已同步至 ESA；`lyric-island.top` 的菜单结构已复核。
- 回滚点：`644a8945f0940e935a831ebe6bb41f588f4f1d70`。
