# 任务交接：Desktop UI / 设置布局保持与逐字歌词流畅度修复

- 日期：2026-09-10
- 基线提交：`c6b439d0f3c691872f69970e7facf88e329913e7`
- 任务范围：Desktop UI 设置运行态同步、逐字歌词呈现、句间切换
- Handoff Status：Implemented / Automated Tests Passed / Packaged as 3.2.44-Beta / Manual UI Pending

## 已完成

- 修复调整“自动缩回”等非布局设置后，运行态同步用旧的 committed `IslandLayouts` 覆盖当前设置草稿，导致歌词岛弹回“水平积木模式”的问题。设置窗口捕获的 `ReadEditedLayoutMode()` 现在可保持到 Apply 后的运行态。
- 逐字歌词在同一句、同一 `LyricLine` 的周期刷新中复用已计算的字形宽度映射，不再每 250ms 重做多个 `FormattedText` 前缀测量；文字、逐字行或字体属性改变时仍会重建映射。
- 在现有 `TimelineCoordinator` 给出的播放位置上，按当前句的下一句时间戳设置一次性 UI 定时刷新，避免逐字进度已走完但句子仍等待常规 250ms 轮询才翻页。暂停、无会话、换歌、无歌词和窗口关闭均停止该定时器。
- 未新增第二套播放位置推算，未修改 QRC/YRC/LRC 解析、歌词缓存或 Core 时间轴语义。

## 修改文件

- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.App/Modules/WordTrackingTextBlock.cs`
- `LyricHover.Tests/Program.cs`
- `docs/coordination/handoffs/2026-09-10-desktop-ui-settings-and-word-following-fixes.md`

## 验证

- `dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:ErrorsOnly`：成功，0 error，245 warnings。
- `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 下执行 Release 测试程序：全部执行项 PASS；新增覆盖包括布局模式保持、同句字形映射复用和句边界刷新。
- `git diff --check`：通过，仅有既有 Windows 行尾转换提示。
- 发布候选：`publish/current/`，版本 `3.2.44-Beta`，递归共 11 个文件、33,809,962 bytes。
- `LyricHover.App.exe` SHA-256：`7B7FFF0DE590C3C58DCDAA7FDCCF435402CF0F96273FC9F50E6A0F1367852D49`。
- `LyricHover.App.dll` SHA-256：`DD84F39D6F4A3D0DCEE08A3E3A897DE86716410BE10AA56782FF48C7E7DA1C97`。

## 未完成与验收边界

- 尚未在真实播放器、真实歌词岛窗口中做连续多句 QRC/YRC 的肉眼流畅度验收；自动化通过不等于真实 UI 验收。
- 已更新 `publish/current`；本交接随功能提交创建，未推送远端。
- 工作树中原有 `docs/desktop-core-domain-knowledge.md` 修改和 `2026-09-06-desktop-ui-tray-menu-v3.2.35-closeout.md` 未跟踪文件不属于本任务，后续提交时不得误纳入。

## 建议验收

- 先选“收缩/展开”布局，修改自动缩回秒数并 Apply，确认不会跳回“水平积木模式”；重启后再次确认持久化结果。
- 选择真实逐字歌词，观察同一句高亮是否连续，以及连续 10 句在词尾到下一句首字之间是否按时间戳切换。
- 分别验证播放、暂停、拖动进度、换歌和手动歌词偏移，确认句边界一次性刷新不会在非播放态推进歌词。
