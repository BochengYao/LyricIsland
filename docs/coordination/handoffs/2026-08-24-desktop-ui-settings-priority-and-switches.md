# 任务交接：Desktop UI / 设置优先级列表与开关状态修复

- 日期：2026-08-24
- 任务线程：Desktop UI & Interaction
- 基线提交：`4decc8e340dfd76d942794bdf128ce8c6d224157`
- 结果提交：未提交；工作区含其他线程 WIP，交接不授权整理或提交它们。
- 当前本地候选包：`publish/current`，`3.1.47-Beta`，`LyricHover.App.exe` SHA-256 `E2CB27FD7430EAA38560E90A18D883DC5C7F0B16975C5419309E2A6ACA8D8491`。
- 允许修改范围：`LyricHover.App/PlacementSettingsWindow.xaml`、`LyricHover.App/PlacementSettingsWindow.xaml.cs`、`LyricHover.App/OverlayPlacementSettings.cs`、`LyricHover.Tests/Program.cs`。

## Task Contract

- Goal：将歌词源、播放器优先级改为 Apple 风格拖动列表；统一设置页字体、标题与间距；修复歌词岛/歌词坞开关的首次状态、双向动画和保底可用性。
- Owner：Desktop UI & Interaction。
- Allowed Write Scope：上述 Desktop UI 设置页与针对性测试。
- Forbidden Scope：不修改播放器/歌词解析语义、Windows Shell 行为、商店身份、用户数据迁移兼容层或共享治理文档。
- Dependencies：`OverlayPlacementSettings` 的持久化字段、设置运行时协调器返回的 effective settings、现有 Release 桌面测试。
- Design Constraints：WPF 原生模板和动画；深浅主题可读；首次绘制必须与 `IsChecked` 一致；动画只服务视觉，不得成为业务状态来源。
- Definition of Done：优先级列表无嵌套滚动；自动播放器选择为开关；标题/小字统一；歌词岛与歌词坞至少一个开启；开关在首次加载与开/关操作时视觉正确。
- Verification：Release 构建、完整桌面测试、`publish.ps1 -NoLaunch` 候选包；真实 WPF 点击与首开仍需人工验收。
- Handoff：本文件交 Desktop UI 复核；配套 Architecture 设计输入见同日 Architecture handoff。

## 已完成

### 优先级设置

- 首选歌词源与播放器选择改为单列卡片式优先级列表，行内提供 `≡` 拖动手柄；使用 `ItemsControl` 与拖放逻辑，不再使用默认 `ListBox` 或嵌套滚动框。
- 播放器“自动选择”位于播放器分区标题右侧，采用开关状态，不再作为独立操作按钮；歌词源不再提供自动选择。
- 移除列表右侧蓝色对勾，避免把排序项误表达为多选项。

### 设置视觉与文案

- 设置项标题统一为白色并对齐“首选歌词源”的视觉权重；小字、导航与控件字体改用 `Segoe UI Variable Text, Microsoft YaHei UI`，调整字号、行高与间距以提高深色主题可读性。
- 恢复“自动折叠”说明；删除“通用页优先选择 XXX”“自动选择会跟随最近活跃的播放器”等冗余说明。
- 设置开关采用统一的 42×24 模板：开启轨道由 `IsChecked` 模板触发器即时设为 `#0A84FF`，不依赖延迟的代码颜色同步。

### 开关状态与动画

- `SyncSwitchVisualState` 在初始同步时直接把圆点设至最终位置；在用户切换时以 160ms `CubicEase(EaseOut)` 动画圆点的 `TranslateTransform.X`。
- 清除已有 WPF 动画时先读取当前渲染坐标并写回 base value，再启动下一个动画，避免关闭时先跳回左侧而看不到回退动画。
- 窗口 `Loaded` 后增加 `DispatcherPriority.Render` 的一次无动画同步，确保歌词坞等已开启的开关在模板完成布局后也显示“蓝轨道 + 右侧圆点”。
- 此同步只修正展示位置；`IsChecked`、运行时状态和持久化仍经既有设置协调器处理。

### 保底启用约束

- UI 事件层：用户关闭歌词岛或歌词坞时，如果两者将同时关闭，自动开启另一项。
- 配置归一化层：读取旧配置或外部写入的“双关闭”状态时，默认恢复歌词岛开启。
- 该约束不改变播放器、歌词源、任务栏安全校验或 LyricDock 失败关闭的原有语义；后者仍可经有效设置回写 UI。

## 验证

- 命令：`dotnet build LyricHover.App\LyricHover.App.csproj -c Release --no-restore`
- 结果：退出码 `0`，`0` warning、`0` error。
- 命令：`$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'; $env:TargetPlatformDisplayName='Windows'; $env:NUGET_PACKAGES='C:\Users\14731\.nuget\packages'; .\publish.ps1 -NoLaunch`
- 结果：退出码 `0`；完整桌面测试输出 PASS，win-x64 发布完成并替换本地 `publish/current`。测试项目仍输出既有 `CS0436` 源文件/程序集重复编译警告，未造成失败。
- 产物：`publish/current/LyricHover.App.exe` ProductVersion=`3.1.47-Beta`，哈希如上。
- 手动 UI：本线程未操纵用户真实桌面窗口；截图暴露的问题已由代码路径定位和修复，但以下场景必须由 UI 线程在真实 WPF 窗口复测。

## UI 复测清单

1. 首次打开设置：歌词岛、歌词坞各自开启时，开关立刻显示蓝轨道及右侧圆点；关闭时为灰轨道及左侧圆点。
2. 连续开/关歌词岛、歌词坞、翻译、省电与自动选择：两个方向均有平滑圆点动画，且不出现先跳位再动画。
3. 在另一表面关闭时关闭歌词岛/歌词坞：另一个表面自动开启；重开设置后状态保持。
4. 拖动歌词源和播放器列表，确认拖动手柄不拖动整个设置窗口、排序平滑、播放器自动选择可点击并持久化。
5. 深色、浅色、系统跟随以及 100%/150% DPI 下检查标题、描述、导航与“自动折叠”说明的可读性和不裁切。

## 未修改 / 非目标

- 未修改 `LyricHover.Core/`、播放器发现、歌词匹配、缓存、时间线和 Windows 任务栏底层安全逻辑。
- 未提交、未上传 GitHub、未提交 Microsoft Store；`publish/current` 仅为本地候选产物。
- 未更新共享架构或决策文档；需要长期化的规则由 Architecture 线程评审。

## 风险与后续

- 已知限制：WPF 开关视觉依赖模板实例和 Render 时机；新增 Render 阶段同步应覆盖首开，但只能以真实窗口复测作为最终证据。
- 交接目标：Desktop UI & Interaction 先完成上述实机验收；若全部通过，发布线程可独立评估候选包，Architecture 线程再决定是否将状态约束升格为 ADR。
- 回滚点：`4decc8e340dfd76d942794bdf128ce8c6d224157`。
