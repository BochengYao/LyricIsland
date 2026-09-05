# 任务交接：Desktop UI & Interaction / 节能模式设置与详情

- 日期：2026-08-25
- 来源线程：节能模式性能优化与本地候选收口
- 结果提交：`47d309ede151bda0c5655ea510337b1f9a60c172`（功能）→ `815444f9363d3567772296924731d472c205f649`（详情文案）
- 当前本地候选：`publish/current`，`3.1.73-Beta`；`LyricHover.App.dll` SHA-256：`40EF97FE89888BAD12347BA09C0CA6C46751C7571C6DE67BAF32208A82DB42D4`。
- 当前工作区含其他线程未跟踪文档；本交接不授权提交、重置、清理或归属它们。

## Task Contract

- Goal：让已存在的 `EnablePowerSavingMode` 同时降低歌词岛与设置窗口的视觉、刷新和布局开销；详情说明必须与实际策略一致。
- Owner：Desktop UI & Interaction。
- Allowed Write Scope：`LyricHover.App` 的设置窗口、主窗口 UI 刷新策略、现有本地化与针对性回归断言。
- Forbidden Scope：不新增设置字段或迁移；不改变 Core 的媒体、缓存、歌词请求或时间线契约；不重做设置系统。
- Definition of Done：设置中的 Draft 开关即时预览视觉策略，Apply 才持久化，Cancel 恢复已接受状态；高对比度和不支持 DWM 的回退不变；详情文字准确、可本地化。

## 已完成行为

### 设置窗口即时预览

- 切换节能开关时，设置窗口立即改用实体背景，停用 Acrylic/Mica、卡片和拖拽预览阴影，并停止循环布局预览与可选过渡动画。
- 这是窗口 Draft 的局部预览，不会提前把 `EnablePowerSavingMode` 写入运行时或持久化设置；Apply/Save 后才应用，Cancel/未应用关闭会恢复已接受设置。
- 高对比度仍优先使用既有无材质回退；DWM 效果不可用时仍为实体背景。

### 主窗口节能策略

- 正在播放或仍需展示时，主刷新间隔为 1 秒。
- 没有媒体会话、歌词岛隐藏、设置未打开、教程未运行且歌词坞未启用时，空闲刷新间隔为 4 秒。
- 媒体会话事件仍即时刷新；鼠标接近检测在节能模式为 500ms。
- 动画模块保持现有开关边界，不影响 Core 侧轮询、缓存或时间线精度。

### 详情与文风更新

节能模式卡片现在使用更简洁、说明优先的文案：

```text
降低能耗，减少不必要的动态效果。

了解节能模式
播放时每秒更新一次；空闲时每四秒检查一次。
设置使用实体背景，并减少阴影、预览和过渡动画。
界面会更安静。歌词切换和悬停将直接响应。
```

繁中、英文、日文均已同步。原“每秒 4 次 / 鼠标每秒 25 次”的实现型旧说明已移除，避免与当前 4 秒空闲策略冲突。

## 相关代码入口

| 文件 | 入口 | 责任 |
| --- | --- | --- |
| `LyricHover.App/PlacementSettingsWindow.xaml` | `PowerSavingModeCheckBox`、`PowerSavingDetailsPanel` | 设置卡片、可展开说明与展示文案。 |
| `LyricHover.App/PlacementSettingsWindow.xaml.cs` | `powerSavingModePending`、`UpdateSettingsPerformanceVisuals` | Draft 即时预览、材质/阴影/动画回退、Apply/Cancel 边界。 |
| `LyricHover.App/MainWindow.xaml.cs` | `GetRefreshTimerInterval`、`IsPowerSavingRefreshIdle` | 1 秒展示刷新、4 秒空闲刷新和鼠标接近检测节流。 |
| `LyricHover.App/UiLanguageService.cs` | 节能模式字符串映射 | 四种界面语言的同步文案。 |
| `LyricHover.Tests/Program.cs` | `PowerSavingModeSchemaDefaultsToDisabled` | 默认关闭、设置详情和视觉策略入口的回归断言。 |

## 验证证据

| 检查 | 结果 |
| --- | --- |
| `dotnet build LyricHover.sln -c Release` | 成功，0 error；测试项目存在 189 条既有 `CS0436` 重复类型警告。 |
| `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1 dotnet run --project LyricHover.Tests -c Release --no-build` | 通过，包括节能模式默认关闭及新增详情断言。 |
| `publish.ps1 -KeepVersion -NoLaunch` | 成功生成同版本本地候选。执行前需 `dotnet restore LyricHover.App\LyricHover.App.csproj --runtime win-x64`，否则当前 SDK 会报 `NETSDK1047`。 |

## UI 复测清单

1. 普通/节能、浅色/深色/高对比度下切换开关：设置窗口应即时预览；材质、阴影和循环预览应正确停用或恢复。
2. 切换后按 Cancel：已接受设置和原视觉必须恢复；按 Apply 后重开窗口必须保持节能策略。
3. 在 100%/150% DPI 展开“了解节能模式”：三段文字应完整换行、无裁切；四种语言均应显示本地化版本。
4. 播放时确认歌词更新节流为约 1 秒；无会话且歌词岛隐藏时确认约 4 秒空闲检查；媒体会话变化仍应即时更新。

## 非目标与接收结论

- 未改变 `LyricHover.Core`、共享接口、设置持久化格式或外部发布渠道。
- 此交接仅供 Desktop UI 后续实机验收和文案维护，状态为 **Knowledge Sync / 无待办代码接管**。
- 未获得完整 DPI、主题和高对比度的实机截图或录屏；如出现视觉问题，由 Desktop UI 采集复现证据后在 App 范围内修复。
