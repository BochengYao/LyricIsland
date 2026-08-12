# 任务交接：基于 main 的干净官网新功能版本提交

- 日期：2026-08-13
- 任务线程：项目统筹与架构（`00-project-coordination`）
- 基线提交：`origin/main` 的 `a77fb0ee7f23594c6b101b3b81f272511a3a5dd5`
- 结果提交：本交接随干净官网候选提交生成；以该提交的最终 SHA 为准。
- 允许修改范围：官网 API、管理端版本字段、公开更新页版本展示、对应四语数据/样式、接口文档、ESA API 测试与本交接记录。

## 已完成

- 从当前 `origin/main` 的干净隔离工作树中重放新功能完整版本字段 API 与管理端提交。
- 仅引入公开更新页显示 `release_version`、按 API 派生 `major_version` 分组、四语回退及其必要样式、预加载和文案依赖。
- API 测试补足既有四语保存断言所缺的输入设置；不改变接口语义。

## 未修改 / 非目标

- 未纳入 `LyricHover.App`、`LyricHover.Core`、桌面测试、`Directory.Build.props`、`CHANGELOG.md`、Store/MSIX、视觉资产或根工作树 WIP。
- 未推送 `origin/main`、未触发 ESA、未执行端到端独立质量复测。

## 验证

- 命令：在 `website/` 执行 `npm run typecheck`、`npm run test:esa-api`、`npm run build:esa`。
- 结果：三项均退出码 `0`；API 测试输出 `ESA API tests passed`；静态构建包含 `/updates`、`/en/updates`、`/zh-hant/updates`、`/ja/updates`。
- 命令：`git diff --check`。
- 结果：待提交前再次执行并记录。

## 风险与后续

- 已知限制：用户已授权跳过独立端到端质量复测；本地门禁通过不等于 ESA 已同步或用户手工验收完成。
- 交接目标：`06-release-github-store` 收到结果 SHA 后，仅推送该提交至 `origin/main`，等待 ESA 自动同步并核验生产四语更新路由与 `/api/features` 的 `release_version`、`major_version`、旧记录回退。
- 回滚点：`a77fb0ee7f23594c6b101b3b81f272511a3a5dd5`；远端回滚须另行授权，不能通过覆盖根工作树实现。
