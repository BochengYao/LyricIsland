# 任务交接：Desktop UI & Interaction / 歌词坞设置、小组件说明与非线性动画

- 日期：2026-08-25
- 来源线程：歌词坞完成收口
- 当前本地候选：`publish/current`，`3.1.72-Beta`；`LyricHover.App.dll` SHA-256：`DC538FA9F271D7D96C3E78996BE971EC06B5D3EECE3E789C0F6CE2EA91E7FC91`。
- 当前工作区含其他线程 WIP；本交接不授权提交、重置或清理任何现有改动。
- 主要修改面：`LyricHover.App/PlacementSettingsWindow.xaml`、`LyricHover.App/PlacementSettingsWindow.xaml.cs`；相关运行时行为见 LyricDock 文件与配套 Architecture handoff。

## Task Contract

- Goal：交接歌词坞设置中小组件提示、Windows 10/11 文字帮助、深色主题可读性以及展开/收起动效的最终 UI 状态。
- Owner：Desktop UI & Interaction。
- Allowed Write Scope：设置窗口 XAML、交互事件和针对性 WPF 验收；保持既有实时设置同步和 Windows 状态检测边界。
- Forbidden Scope：不重新设计整套设置系统；不改 Windows 小组件底层自动化、歌词解析、播放器策略、Store 身份或发布渠道。
- Definition of Done：用户可发现小组件影响、可展开得到文字路径、深色主题可读、两向动效连续且不跳帧；歌词坞右键和设置入口不回退到过期“任务栏歌词”叫法。

## 已完成 UI 行为

### 歌词坞说明与帮助

在“开启歌词坞”开关下方显示：

```text
开启后会关闭系统任务栏小组件，重新打开小组件…
```

“重新打开小组件…”为蓝色可点击控件；点击后展开下方帮助，分别以文字说明 Windows 11 与 Windows 10 恢复小组件的路径。此前使用 `TextBlock.Inlines` 时会被本地化服务重写清空，现已改为独立控件，避免整段说明消失；此前点击又被窗口拖动预处理吞掉，现通过真实 `Button` 保证事件触发。

### 深色主题可读性与版式

- 说明和帮助正文使用 13pt，避免被“12.5 以下视为弱文案”的全局主题处理压暗。
- 展开面板使用 `SettingsControlBackgroundBrush`，搭配 `SettingsControlForegroundBrush` 与边框资源，而非浅灰按下态背景。
- 主说明与蓝色链接位于两列 Grid，保持基线对齐；帮助内容拆为两个正常 TextBlock，避免翻译/内联内容破坏换行。

### 两向非线性动画

- 展开：高度 `0 → 88`，260ms `QuarticEaseOut`；透明度 `0 → 1`，190ms 同曲线。
- 收起：高度以面板 **当前实际渲染高度** 为起点 `→ 0`，220ms `CubicEaseIn`；透明度从当前值 `→ 0`，180ms 同曲线。
- 收起完成后才将面板设为 `Collapsed`。

修复的根因：旧逻辑在收起前 `BeginAnimation(..., null)`，WPF 会立即还原 XAML 的 `MaxHeight=0`，所以面板会先跳零、再显示几乎不可见的收起动画。当前逻辑先读取 `ActualHeight`/`Opacity`，清除旧动画后把当前值写回 base value，再启动反向缓动。快速连续点击时也应从当前状态自然衔接，而非两次跳位。

## 相关代码入口

| 文件 | 入口 | 责任 |
| --- | --- | --- |
| `LyricHover.App/PlacementSettingsWindow.xaml` | `WidgetsHelpLink`、`WidgetsHelpPanel` | 文案、主题资源、布局、初始裁切状态。 |
| `LyricHover.App/PlacementSettingsWindow.xaml.cs` | `WidgetsHelpLink_Click` | 保存当前渲染值并启动展开/收起动画。 |
| `LyricHover.App/PlacementSettingsWindow.xaml.cs` | `Window_MouseLeftButtonDown` | 不得重新吞掉真实按钮的点击事件。 |
| `LyricHover.App/LyricDock/*` | 小组件状态、回退和检测 | 运行时边界；详见 Architecture handoff。 |

## 验证证据

- Release 编译：`dotnet build LyricHover.App\LyricHover.App.csproj -c Release --no-restore`，退出码 `0`，0 warning、0 error。
- 本地包已重新输出到 `publish/current`；不等于上传、审核或正式发布。
- 本线程根据用户实机反馈连续修正了文本缺失、对齐、暗色可读性与收起跳变。最后一次收起修复已完成编译，但尚未获得该最后构建在用户桌面上的录屏/截图证据。

## UI 复测清单

1. 在深色主题下打开歌词坞设置：主说明、蓝色链接、Windows 10/11 两行帮助均应清晰可读，不裁切、不使用浅灰大面板。
2. 连续点击“重新打开小组件…”至少五次：展开应先快后慢；收起应从当前高度自然加速收拢，没有先跳零、卡两下或空白闪烁。
3. 在展开过程的一半再次点击：面板应从当前高度反向收起；在收起过程的一半再次点击：应重新平滑展开。
4. 在 100% 和 150% DPI、深色/浅色/系统跟随下检查换行与对齐；Windows 10 文案不超出面板。
5. 开启歌词坞：确认小组件状态变化过程不阻塞设置窗口，且后台失败时的确认弹窗仍使用深色设置界面语言；确认产品全程显示“歌词坞”。

## 非目标与后续

- 这次没有重做设置布局、未增加图形资产、未改变 Windows Shell 自动化策略。
- 如验收仍出现掉帧，优先采集 UI 线程耗时与真实渲染帧证据，再判断是否需要将 `MaxHeight` 动画改为 `RenderTransform`；不要仅凭主观速度直接扩大改动范围。
- 版本策略：`3.1.72-Beta` 作为当前歌词坞批次维持不变；同批次 UI 修复覆盖本地包时不重复递增。完整批次验收后才由 Release 决定下一公开版本。
