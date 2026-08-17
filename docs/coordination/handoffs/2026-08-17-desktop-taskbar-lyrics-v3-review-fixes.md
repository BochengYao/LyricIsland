# 任务交接：V3 任务栏歌词独立复核修正

- 日期：2026-08-17
- 任务线程：04-desktop-island-settings-ui
- 基线提交：a745c32770aa6ff1830252c6e512521b8d82b195
- 结果提交：见本交接文件所在的新增提交（未 amend 基线提交）
- 允许修改范围：LyricHover.App 的任务栏歌词、安全设置交互与 App 范围回归测试。

## 已完成

- 移除固定 `leftReserved/rightReserved`。Windows 环境通过 UI Automation 读取目标屏幕真实 `Shell_TrayWnd` / `Shell_SecondaryTrayWnd`、Widgets 按钮矩形与任务栏可交互控件矩形；仅从不与 Start/Search/固定应用/托盘控件重叠的连续空闲区选择 220–360px 位置，并支持居中、左对齐。
- 找不到真实 Widgets 锚点、目标任务栏、空闲区不足、Explorer 句柄变化或 DPI/任务栏无效时 fail-closed；自动隐藏与全屏覆盖仅隐藏窗口，其他不可靠状态会关闭功能并恢复 Widgets。
- `TaskbarDa` 写入后广播并轮询验证：首次禁用必须实际观察 Widgets UIA 矩形消失；不是只接受广播返回。租约保留 absent/0/1，恢复文件使用原子写，所有目录/读写/删除异常均捕获并失败关闭。
- 任务栏表面改为轻量原生文本窗：使用现有 App 图标、深浅任务栏前景、长行横滚、无独立背景、`WS_EX_NOACTIVATE`、左键无操作、右键定位设置；不再复用岛屿 `LyricsModuleView`。
- 任务栏只消费真实播放文本；教程文案只留在顶部岛。无会话时明确显示“等待播放”。
- 设置新增居中/左对齐和首次确认对话；取消不会提交开关。启用失败或运行时锚点失效会持久化回 `TaskbarLyricsEnabled=false` 并给出具体原因。

## 未修改 / 非目标

- 未修改 Core 语义、媒体轮询、歌词解析/缓存、发布脚本、版本文件、Store 身份或 publish/current。
- 未生成候选包，未上传 Store。

## 验证

- 命令：`dotnet build LyricHover.sln -c Release`
- 结果：通过，0 警告、0 错误。
- 命令：`dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：新增/更新测试通过：租约 absent/0/1、刷新失败回滚、IO fail-closed、Widgets 锚点拒绝与恢复、共享快照、220–360 宽度、真实矩形安全区左右对齐、确认/非激活/主题声明。完整回归仍仅有两个既有失败：`translation mode explains why single line is unavailable`、`mouse avoidance settings fit without scrolling`。
- 手工/UI：当前会话没有可受控的 Win11 真实任务栏截图会话；未伪造截图或声称通过实机验收。

## 风险与后续

- 已知限制：必须由 05-quality-integration 在真实 Win11 覆盖以下矩阵：主/副任务栏、Widgets 已启用和初始已禁用、居中/左对齐、100/125/150/200% DPI、浅/深主题、自动隐藏、全屏覆盖、Explorer 重启、屏幕插拔和不同 Start/Search/固定项/托盘密度。
- 交接目标：05-quality-integration 完成上列矩阵，并特别核对 UI Automation 在中/英/日系统的 Widgets 元素 AutomationId/名称是否可定位；若存在无法可靠定位的 Windows 构建，应保持拒绝启用。
- 回滚点：a745c32770aa6ff1830252c6e512521b8d82b195；更早 V3 基线：e07a72db0bb77faa519fe594b973d0fc57855637。
