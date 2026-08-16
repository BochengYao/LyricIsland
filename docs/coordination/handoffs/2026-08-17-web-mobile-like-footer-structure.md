# 任务交接：移动端激励卡片三段式结构

- 日期：2026-08-17
- 任务线程：01-web-frontend
- 基线提交：`0c7b461f812aa76c565db36663975fcf5a72685f`
- 结果提交：见包含本交接文件的提交
- 允许修改范围：公开 `/incentives` 必要组件与共享公开样式

## 已完成

- 将激励卡片明确拆成 `acceptedCardHeader`、可伸缩且可裁切的 `acceptedCardContent`、正常文档流中的 `acceptedMeta` footer。
- 移动端卡片使用 `auto / minmax(0, 1fr) / 44px` 三行网格；长标题和正文只在自己的区域省略，无法侵入 footer。
- 作者和点赞按钮在固定 44px footer 中并排；点赞保持完整 `52×44px` 点击盒和现有 `52×26px` 短扁视觉胶囊。
- 取消按钮绝对定位，短内容和长内容均不会导致 footer 漂移。

## 未修改 / 非目标

- 未修改后台、API、数据、鉴权、公开数据契约或桌面端。
- 未触发 ESA；由发布统筹按用户延续授权直接发布。

## 验证

- `npm run typecheck`：通过。
- `npm run test:esa-api`：通过，输出 `ESA API tests passed`。
- `npm run build:esa`：通过，生成 16 个静态页面。
- 使用本机 Microsoft Edge 和实时 `https://lyric-island.top/api/incentives/public` 数据验证；最长生产样本标题 18 字、正文 39 字。
- 截图视口：`320×720`（0/17000ms）、`390×844`（0/12000/26000ms）、`844×1100`、`1440×900`。动画均在实际旋转状态暂停后截图。
- 320/390 全部渲染卡片断言：header/content/footer 两两不交叉；正文/作者/点赞两两不交叉；点击盒为 `52×44px` 且位于卡片内；footer 固定 44px；横向 overflow 为 0。
- 黄色、蓝色、白色以及左右边缘卡片均出现在截图样本中。
- 证据目录：`C:\Users\14731\.codex\visualizations\2026\08\16\01a009c2-16ab-7722-9c13-d766a95fcbc6\like-footer-0c7b461`，汇总断言见 `metrics.json`。

## 风险与后续

- 已知限制：无代码级阻塞；生产发布后仍需由发布线程核验实际路由。
- 交接目标：发布统筹 / 06-release-github-store，按用户授权直接触发 ESA，不等待质量线程。
- 回滚点：`0c7b461f812aa76c565db36663975fcf5a72685f`。
