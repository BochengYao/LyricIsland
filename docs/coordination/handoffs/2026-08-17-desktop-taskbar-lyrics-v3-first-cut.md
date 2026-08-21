# 任务交接：V3 任务栏歌词首版

- 日期：2026-08-17
- 任务线程：04-desktop-island-settings-ui
- 基线提交：e07a72db0bb77faa519fe594b973d0fc57855637（父链：6941e86 → ece2c8c → e07a72d）
- 结果提交：见本交接文件所在的独立功能提交
- 允许修改范围：LyricHover.App 的任务栏歌词窗口、控制器、设置与 App 范围自动测试。

## 已完成

- 增加无边框、不可抢焦点、无任务栏图标的 `TaskbarLyricsWindow`；左键无操作，右键打开并聚焦“任务栏歌词”设置。
- 新增可替换 `ITaskbarEnvironment`、`WindowsTaskbarEnvironment`、`WidgetVisibilityLease` 与 `TaskbarLyricsController`。注册表仅访问 HKCU 的 `TaskbarDa`，按 absent/0/1 原值租约恢复；失败回滚，启动时先恢复残留租约，退出时恢复。
- 任务栏窗复用 `LyricsModuleView` 的横滚；主窗口一次创建 `LyricsPresentationSnapshot`，同时驱动顶部岛和任务栏歌词，不新增媒体轮询、歌词解析或缓存。
- 设置 schema 升至 3，`TaskbarLyricsEnabled` 默认 false；旧设置加载后持久化升级。首次开启说明 Widgets 临时隐藏与 fail-closed 条件。
- Win11 实现会在无匹配任务栏、自动隐藏、全屏覆盖、可用宽度低于 220px 或无效 DPI/高度时隐藏窗口；窗口宽度限制为 220–360px。

## 未修改 / 非目标

- 未修改 LyricHover.Core 语义、Directory.Build.props、发布脚本、版本文件、Store 身份或任何发布目录。
- 未生成发布候选、未打包、未上传 Store。

## 验证

- 命令：`dotnet build LyricHover.sln -c Release`
- 结果：通过，0 警告、0 错误。
- 命令：`dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：新增任务栏租约 absent/disabled/enabled、刷新失败回滚、共享快照/宽度、schema 默认关闭与旧设置兼容测试均通过。完整套件仍有两个既有失败：`translation mode explains why single line is unavailable`、`mouse avoidance settings fit without scrolling`；与本次任务栏变更无关。
- 手工/UI：本轮环境未提供可截图的受控 Win11 任务栏交互会话，未伪造截图或宣称实机通过。

## 风险与后续

- 已知限制：仍需 Win11 实机逐项验收主/副屏、左对齐/居中、DPI、自动隐藏、Explorer 重启、全屏窗口、不同任务栏固定项布局，以及实际 Widgets 刷新是否立即生效；任一无法安全定位的情况应保持隐藏并恢复 Widgets。
- 交接目标：05-quality-integration 完成上述 Win11 实机门禁并确认两项既有回归失败的基线归属；发布线程仅在门禁结论明确后处理候选。
- 回滚点：e07a72db0bb77faa519fe594b973d0fc57855637。
