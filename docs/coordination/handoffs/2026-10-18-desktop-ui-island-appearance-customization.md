# 歌词岛外观自定义交接

## Result

实现歌词岛中英文字体、字号、上方凹凸角型、凹角/凸角半径和恢复默认半径。版本配置为 `3.3.0`，程序集/文件版本为 `3.3.0.0`。

## Changed Files

- `Directory.Build.props`：将版本源改为 `3.3.0`，移除 Beta 后缀。
- `LyricHover.App/OverlayPlacementSettings.cs`：增加兼容可选的外观字段和边界规范化。
- `LyricHover.App/IslandAppearanceRendering.cs`：共享字体混排、系统字体回退和岛体几何实现。
- `LyricHover.App/Modules/WordTrackingTextBlock.cs`、`LyricsModuleView.xaml(.cs)`、`IslandModuleHost.xaml.cs`：应用混排字体/字号并在字体变化后重测布局；逐字跟随仍按原字符串索引计算。
- `LyricHover.App/MainWindow.xaml.cs`：实际岛体使用共享几何，并将同一几何用于内容裁剪。
- `LyricHover.App/PlacementSettingsWindow.xaml(.cs)`、`UiLanguageService.cs`：在歌词岛原设置列表新增控件、实时预览、恢复默认半径及四语文案；设置在 220 ms 后自动提交，关闭窗口会先刷新待提交值。
- `LyricHover.Tests/Program.cs`：增加旧配置默认、范围规范化、序列化往返、胶囊轮廓和中英日混排回退覆盖，并更新 3.3.0 版本断言。

## Verification

- Release 构建：`dotnet build LyricHover.sln -c Release --no-restore`，0 错误、310 警告。
- 桌面回归：同一 Release 构建执行 `LyricHover.Tests.dll`，272 PASS、0 FAIL。发布版本生成并发夹具因当前环境没有可读 NuGet 全局缓存而通过 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 跳过；直接运行该夹具时报告缓存路径 `C:\Users\CodexSandboxOffline\.nuget\packages` 不可读。
- `git diff --check`：通过。
- 真实 UI 验收尚未完成：未记录 100%/150%/200% DPI、多屏、拖动/命中/悬停、实际字体回退与重启视觉结果。

## Known Limitations

- 仍需在真实 Windows UI 中按任务书检查主/副屏、100%/150%/200% DPI、单/多行、收起/展开、缺字字体、胶囊轮廓和恢复默认半径，并记录预览与实机表现。
- 构建的 310 条警告主要包含测试项目链接的 LyricDock 类型与 App 程序集同名冲突；构建无错误。

## Cross-module Impact

- Core 歌词解析、时间线与播放器行为没有改动。
- LyricDock 外观和定位没有改动；外观配置只由 IslandModuleHost 中的歌词模块消费。
- Store 标识、单实例标识、用户数据路径和发布脚本没有改动。

## Commit SHA

未提交；没有 Commit SHA。

## 2026-10-19 外观问题修正

- 修正默认轮廓左下凸角的路径方向与贝塞尔控制点，避免底部轮廓形成尖刺或扩大成三角形。
- 新增可选设置 `IslandConvexScreenEdgeDistance`。凸角模式下，原“凹角半径”控件动态切换为“屏幕边缘距离”；切回凹角模式时恢复独立保存的凹角半径。
- 扩宽并居中字号/半径数值输入框，覆盖 120 DIP 的三位数显示。
- 回归验证：Release 桌面测试 272 PASS、0 FAIL；`win-x64` Release 构建 0 警告、0 错误。
- 更新候选生成到 `publish/candidates/v3.3.0-screen-edge-distance`。因 `publish/current/LyricHover.App.exe` 当时正在运行，为保留该进程未替换 `publish/current`；关闭该实例后可将此候选提升为 current。
- 本次仍未完成真实 Windows UI 的多屏和 DPI 手工验收。

## 2026-10-19 右键崩溃修正

- Windows Application Error / .NET Runtime 事件确认 `3.3.0.0` 在鼠标源为 `System.Windows.Documents.Run` 时，`IsInteractiveMouseSource` 调用 `VisualTreeHelper.GetParent` 抛出未处理的 `InvalidOperationException`；移动到歌词文字后右键会触发该输入路径。
- 新增兼容 Visual、Visual3D 与 ContentElement 的鼠标源父节点遍历，并让交互按钮和模块命中检测共用该遍历。
- 新增 Run 文本节点及其按钮祖先识别回归；桌面测试 273 PASS、0 FAIL；Release 解决方案构建 0 警告、0 错误。
- 已更新 `publish/current` 为 `3.3.0.0` 修正版。替换前程序未运行；旧产物归档在 `publish/archive/v3.3.0-before-right-click-fix`。

## 2026-10-19 半径有效范围与屏幕边缘距离

- 凹角半径/凸角半径滑块的最大值现在按预览尺寸及当前几何计算，滑块不会再保留超出轮廓实际可见变化范围的长尾。
- 凸角模式下的“屏幕边缘距离”现在调整窗口整体位置，预览同步留出距离；岛体高度和轮廓不再被距离值压缩。隐藏位置保持在屏幕外。
- Release 解决方案构建成功（0 错误；已有测试项目同名类型冲突等警告）。本轮未运行回归测试，也未更新 `publish/current`；真实桌面视觉验收仍待确认。

## 2026-10-19 v3.3.1 本地候选

- 版本递增到 `3.3.1`，程序集/文件版本 `3.3.1.0`。
- 桌面回归 273 PASS / 0 FAIL；`win-x64` Release 构建 0 警告、0 错误。
- `publish/current` 已更新：8 个文件、24,599,011 bytes；`LyricHover.App.dll` SHA-256 为 `237D993CA44A988656B506C818EA4F3F567E992ED16A0963BA630342ED744C7B`。旧候选归档至 `publish/archive/v3.3.0-Beta-20261019-130121`。
- 发布脚本末尾写 `publish/README.md` 时发生访问拒绝；已手动把 README 当前版本与产物对齐为 `v3.3.1`。候选程序没有自动启动，真实桌面视觉验收待执行。

## 2026-10-19 v3.3.2 本地候选

- 修正预览文字居中：预览区域因凸角边缘距离扩高时，文字只补偿一半的扩高量，使文字中心与下移后的岛体中心一致。
- 上方角型状态改为使用独立模式字段；切换角型立即提交运行时设置，关闭窗口不再依赖待触发的延迟保存；增加重启持久化回归覆盖。
- 选项改为“凹角 / 凸角”，四种语言均移除胶囊括注。
- 版本 `3.3.2`，程序集/文件版本 `3.3.2.0`；桌面回归 274 PASS / 0 FAIL；`win-x64` Release 构建 0 警告、0 错误。
- `publish/current`：8 个文件、24,599,075 bytes；`LyricHover.App.dll` SHA-256 为 `4F2DB9D1D7EBB21C6D340615486EE6B7092546C6B24E547A3F1F3B6B5FD74156`。旧候选归档至 `publish/archive/v3.3.1`。README 当前版本已对齐为 `v3.3.2`；新版未自动启动，真实桌面视觉验收待执行。

## 2026-10-19 v3.3.3 本地候选

- 当前设置页改动打包为 `3.3.3`，程序集/文件版本 `3.3.3.0`。
- Release `win-x64` 发布成功；本次未运行回归测试，也未启动候选程序。
- `publish/current`：8 个文件、24,600,195 bytes；`LyricHover.App.dll` SHA-256 为 `0C1D35A3F39D5C511254DDF4A2C9348B2FF45D6BCE8003239597142DE9D40CCB`。旧候选归档至 `publish/archive/v3.3.2-20261019-153314`；README 版本已对齐为 `v3.3.3`。
- 本地候选不代表已提交 Microsoft Store；真实 Windows UI 验收仍待完成。

## 2026-10-19 v3.3.4 本地候选

- 字体下拉滚轮改为在 Popup 内滚动字体列表并拦截外层滚动；字体选择后立即提交当前选择。
- 字体设定应用于歌词、歌曲信息和进度时间；字号改为 0.5–3.0 倍缩放，保留模块原字号比例。设置 SchemaVersion 升至 7，旧版绝对字号按 15 DIP 基准换算；继续同步旧字段以兼容回退版本。
- 缩小字体触发的歌词岛尺寸动画缩短至 140ms；预览移到中文字体上方，恢复默认半径按钮移到预览右侧。
- Release `win-x64` 发布成功；应用项目 Release 编译 0 警告、0 错误。本次未运行桌面回归测试，未启动候选程序。
- 最终 `publish/current`：8 个文件、24,605,707 bytes；`LyricHover.App.dll` SHA-256 为 `C323B7DCE2E7409DBA975D1D6B7E8EF9E2B56D16F18B3955DE1E10109E6459AF`。旧候选归档至 `publish/archive/v3.3.3-20261019-160326`，初次 3.3.4 候选归档至 `publish/archive/v3.3.4-before-font-scroll-adjustment-20261019-160500`，行高修正候选归档至 `publish/archive/v3.3.4-before-preview-row-fix-20261019-160804`；README 已对齐为 `v3.3.4`。
- 真实 Windows UI 验收仍待执行；本地候选不代表已提交 Microsoft Store。
