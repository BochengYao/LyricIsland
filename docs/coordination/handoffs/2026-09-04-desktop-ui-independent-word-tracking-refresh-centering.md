# 任务交接：Desktop UI / 歌词岛与歌词坞独立逐字追踪、强刷与居中修复

- 日期：2026-09-04
- 来源线程：逐字歌词追踪接入、翻译/强刷恢复与显示修复
- 交接目标：Desktop Island, Settings & Interaction UI
- Handoff Status：**Feature Handoff / 待 UI 实机验收**
- 基线提交：`815444f9363d3567772296924731d472c205f649`
- 结果提交：未提交；当前为 `main` 上的混合未提交工作树，不能由结果 SHA 审计。
- 当前本地候选：`publish/current`，`3.2.32-Beta`，framework-dependent `win-x64`。
- 候选哈希：`LyricHover.App.dll` SHA-256 `D136F5B44EA22EDE421C0F9E0683526AC88613217966E4B5B1BEE9C57F3B2C1E`。
- 渠道状态：仅已构建、已测试、已打包到本地；未上传、未提交或发布到 GitHub、Microsoft Store、官网等外部渠道。

## Task Contract

- Goal：歌词岛和歌词坞分别控制是否消费真实逐字时间；恢复 Ctrl+左键和设置页强制刷新；翻译继续整行显示；逐字高亮平滑且换行居中稳定。
- Owner：Desktop Island, Settings & Interaction UI。
- Allowed Write Scope：`LyricHover.App/` 的设置模型消费、设置页、主窗口交互、歌词岛模块、歌词坞表面和展示快照，以及 UI 直接回归测试。
- Forbidden Scope：不在 UI 中解析 QRC/YRC，不复制 Core 时间线，不重做设置系统，不改变歌词坞 Windows Shell 安全边界。
- Definition of Done：两个开关默认开启且独立持久化；Apply/Cancel 遵循 Draft → Runtime → Persist；暂停/跳转/偏移即时同步；无逐字数据回退整行；连续长短歌词不再使用上一行宽度居中。

## 已完成

### 1. 两个独立设置

- `OverlayPlacementSettings` 新增 `IslandWordTrackingEnabled` 和 `LyricDockWordTrackingEnabled`，默认均为 `true`。
- 设置页在歌词岛和歌词坞卡片中分别提供“逐字追踪”开关，互不联动。
- 开关值沿用现有 UI Draft、运行时同步和持久化流程；Cancel 丢弃 Draft，Apply 后同步运行时并保存。
- 从“两处都关闭”切换到“任一开启”时，当前曲目会强制刷新以尝试将旧 LRC 缓存升级为逐字歌词包。

相关文件：

- `LyricHover.App/OverlayPlacementSettings.cs`
- `LyricHover.App/PlacementSettingsWindow.xaml`
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`
- `LyricHover.App/MainWindow.xaml.cs`

### 2. 歌词岛与歌词坞逐字展示

- 播放快照携带 `PrimaryWordTrackingLine`、`WordTrackingPosition` 和回退进度。
- 歌词岛、歌词坞只在各自开关开启时接收逐字数据；翻译/第二行仍为普通整行文本。
- `WordTrackingTextBlock` 使用低亮度底层文字和主题高亮覆盖层，通过 `RectangleGeometry` 裁切显示已唱部分。
- 播放中基于 `CompositionTarget.Rendering` 和 Core 提供的真实词时间逐帧投影，避免“一词一跳”；暂停、跳转、切歌或无有效逐字行时立即重锚或恢复整行样式。
- 现有淡入淡出、跑马灯、单双行和翻译显示路径保留。

相关文件：

- `LyricHover.App/Modules/WordTrackingTextBlock.cs`
- `LyricHover.App/Modules/IslandRenderState.cs`
- `LyricHover.App/Modules/LyricsModuleView.xaml`
- `LyricHover.App/Modules/LyricsModuleView.xaml.cs`
- `LyricHover.App/LyricDock/LyricsPresentationSnapshot.cs`
- `LyricHover.App/LyricDock/LyricDockWindow.cs`

### 3. 强制刷新恢复

- 设置页“刷新当前歌词”会调用 `RefreshCurrentTrackLyrics(true)`。
- 歌词岛在临时交互键按下时左键点击会强制刷新；默认临时交互键为 Ctrl，并继续尊重用户修改后的配置。
- 歌词坞将同一交互转为 `RefreshRequested`，由 `MainWindow` 在 UI Dispatcher 上执行同一强制刷新入口。
- 强刷先显示“正在搜索同步歌词...”，重新请求后仅在获得非空歌词包时覆盖缓存。

相关文件：

- `LyricHover.App/HotkeySettings.cs`
- `LyricHover.App/MainWindow.xaml`
- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.App/LyricDock/LyricDockController.cs`
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`

### 4. 主歌词偶发不居中修复

- 原因是逐字控件用 CLR 属性转发两层 `TextBlock.Text`，外层同步测量偶尔仍读到上一句的 `DesiredSize`。
- 文本变化后现会显式使两个内部文本层和包装控件的 Measure 失效，确保同一轮 `Canvas.Left` 计算使用当前句宽度。
- 新增连续“长句 → 短句 → 另一短句”测量回归，覆盖截图中第一行偏左、第二行仍居中的现象。

相关文件：

- `LyricHover.App/Modules/WordTrackingTextBlock.cs`
- `LyricHover.App/Modules/LyricsModuleView.xaml.cs`
- `LyricHover.Tests/Program.cs`

## 验证证据

| 命令 / 检查 | 结果 |
| --- | --- |
| `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1 dotnet run --no-restore --configuration Release --project LyricHover.Tests` | 所有执行的产品回归均 PASS；包含 QRC/YRC、独立开关、快照、平滑裁切和当前句宽度居中。测试工程仍有既有 `CS0436` 警告。 |
| `dotnet build --no-restore --configuration Release --runtime win-x64 LyricHover.App\LyricHover.App.csproj` | 成功，0 warning、0 error。 |
| `dotnet publish --no-restore --configuration Release --runtime win-x64 --self-contained false ...` | 成功生成 `publish/current` 的 `3.2.32-Beta` 本地包。 |
| 版本与哈希检查 | `LyricHover.App.dll` ProductVersion `3.2.32-Beta`；SHA-256 `D136F5B44EA22EDE421C0F9E0683526AC88613217966E4B5B1BEE9C57F3B2C1E`。 |

## UI 实机复测清单

1. 歌词岛开、歌词坞关；歌词岛关、歌词坞开；两者都开：逐字追踪应分别服从自己的开关。
2. QQ/网易各播放一首有逐字数据的歌曲：主行高亮应按真实词时长连续滑动，翻译整行不裁切。
3. 播放一首只有 LRC 的歌曲：不应显示“未找到同步歌词”，应按现有逐行样式显示。
4. 暂停、恢复、拖动进度、切歌、修改歌词偏移：高亮位置应立即重锚，不继续旧行投影。
5. 连续播放长短差异明显的歌词，确认第一行和第二行均以歌词模块中心为基准，不再继承上一句宽度。
6. 分别在歌词岛和歌词坞按住默认 Ctrl 后左键：当前曲目应进入“正在搜索同步歌词...”并重新请求；修改临时交互键后应服从新按键。
7. 设置窗口分别修改两个开关：Cancel 后恢复原值，Apply 后重开设置仍保留；从两者都关到任一开启应刷新当前曲目。
8. 在 100%/150% DPI、单行/双行、翻译开/关及跑马灯场景复核裁切边界和居中。

## 未修改 / 非目标

- 未让翻译逐字高亮。
- 未改变歌词坞位置安全、Widgets 租约或非激活窗口策略。
- 未新增独立 UI 时间线；所有逐字进度来自 Core 行数据和现有有效播放位置。
- 未完成上述完整实机矩阵的截图/录屏证据，自动化不能替代视觉验收。

## 风险与后续

| Evidence | Impact | Suggested Owner |
| --- | --- | --- |
| 当前只有自动化测量回归，没有修复后同场景截图 | 居中修复已由布局数据证明，但仍缺 DPI/字体渲染的可观察证据 | Desktop UI + Quality |
| `CompositionTarget.Rendering` 在播放逐字行时逐帧回调 | 双表面同时开启会增加少量 UI 帧工作，需要在节能模式和低端设备观察 | Desktop UI |
| 当前工作树混有 Core、UI、版本和其他未跟踪文档 | 不得由 UI 线程直接全量提交或重置 | Integration |
| `publish/current` 当前未启动 | 已打包不等于完成用户侧实机播放验收 | Desktop UI / User Acceptance |

## 回滚点

- UI 可将两个逐字开关关闭并让快照不携带逐字行，即时退回普通整行显示。
- 若回滚 `WordTrackingTextBlock`，需同步恢复歌词岛和歌词坞的普通 `TextBlock`，并保留强制刷新入口。
- 不得单独回滚测量失效修复而保留逐字包装控件，否则偶发水平偏移会重新出现。
