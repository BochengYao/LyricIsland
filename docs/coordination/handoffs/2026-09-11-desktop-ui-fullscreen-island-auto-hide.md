# 任务交接：全屏时自动隐藏歌词岛

- 日期：2026-09-11
- Owner：Desktop Island, Settings & Interaction UI
- 起始基线：`652f366`
- 功能分支：`codex/feature/desktop-fullscreen-auto-hide`
- 功能提交：`cd266760caac9d1afc4bf4295c09d339d8ba5b74`
- 状态：实现、自动化验证与本地 `3.2.45-Beta` 候选打包完成；真实窗口视觉和全屏切换验收待执行
- 允许范围：`LyricHover.App/`、相关 `LyricHover.Tests/` 回归与本交接

## Result

偏好设置的“歌词岛”卡片新增“全屏时自动隐藏”开关，默认关闭。旧设置文件缺失该字段时继续保持原有行为；开启并应用后，当前台窗口完整覆盖歌词岛所在显示器时临时收起歌词岛，退出全屏后按当时的播放、暂停和无播放可见性状态恢复。

歌词岛与歌词坞复用 `ForegroundFullscreenDetector` 的同一套屏幕覆盖判定。探测失败或前台为 Windows 桌面 Shell 时保持显示，不因不可靠环境信息误隐藏。全屏抑制只改变运行时可见性，不改写 `IslandEnabled` 或播放/歌词状态。

## Changed Files

- `LyricHover.App/OverlayPlacementSettings.cs`：新增默认关闭的 `HideIslandInFullscreen` 持久化字段。
- `LyricHover.App/PlacementSettingsWindow.xaml(.cs)`：新增开关、工作草稿捕获、自动 Apply 和有效设置回显。
- `LyricHover.App/UiLanguageService.cs`：新增简体中文、繁体中文、英语和日语文案。
- `LyricHover.App/ForegroundFullscreenDetector.cs`：新增可复用的前台窗口/目标显示器全屏判定。
- `LyricHover.App/LyricDock/WindowsLyricDockEnvironment.cs`：改为复用统一全屏判定。
- `LyricHover.App/MainWindow.xaml.cs`：新增 300 ms 全屏状态监视；省电模式下降为 1 s；保留全屏期间的逻辑显示意图并在退出时按当前状态恢复。
- `LyricHover.Tests/Program.cs`：新增兼容加载/持久化、多语言、整屏几何和运行态接线回归。

## Verification

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:ErrorsOnly
dotnet run --project LyricHover.Tests -c Release --no-build -- --settings-runtime-state-fixture
$env:LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE='1'
& .\LyricHover.Tests\bin\Release\netcoreapp3.1\LyricHover.Tests.exe
```

- Release 构建：退出码 0，0 error，253 warnings。
- 设置定向夹具：6/6 PASS。
- 完整桌面回归：退出码 0，256 PASS、0 FAIL。
- `git diff --check`：通过，仅有既有 CRLF 行尾提示。

## Local Candidate

- 路径：`D:\AppleMusicDesktopLyrics\publish\current`
- 版本：`3.2.45-Beta`
- 文件：11 个，共 33,817,842 bytes
- `LyricHover.App.exe` SHA-256：`C1A8336BB754CCF5C2CDCF2536926A76F240EE3B59F05300E9C1E02AE504D96B`
- `LyricHover.App.dll` SHA-256：`4BDA5B1AFE18EC5DA4C1D82D0A9CDDD0D1F67DA246850166C2FB3F8A863D2AE2`
- `LyricHover.Core.dll` SHA-256：`E01B53D465CBCFAB6C0CE1BBEF603EB5D221984F09F72B1A524E05C0419E863D`
- App/Core DLL 均与最终 win-x64 Release 构建输出一致；使用 `-NoLaunch`，未自动启动。
- 候选构建输入与功能提交一致；工作区中未提交的宣传图和 Canva 输出不属于构建输入，也未纳入功能提交。

## Manual Acceptance Pending

Computer Use 初始化连续两次因本地插件依赖 `tslib.es6.js` 缺失而失败，依技能恢复规则停止重试。因此本线程未完成真实 WPF 视觉和真实全屏应用切换验收。

人工验收应覆盖：

1. 打开“偏好设置 → 歌词外观”，确认新增行未裁切，四种语言下标签与提示可读。
2. 默认关闭时进入浏览器/视频播放器全屏，歌词岛保持原行为。
3. 开启后，在歌词岛所在显示器进入全屏，300 ms 内收起；退出后播放中的歌词岛恢复。
4. 全屏期间暂停并超过收起宽限，或停止播放并超过无播放倒计时，退出全屏后不得错误弹出。
5. 双显示器下，只在完整覆盖歌词岛所在显示器时收起；另一显示器全屏不影响歌词岛。
6. 重启应用确认开关值保留。

## Scope and Impact

- 未修改 `LyricHover.Core/`、播放器、歌词、缓存或时间线语义。
- 未修改 Store 标识、单实例名称、数据目录迁移或发布脚本。
- 已将版本源递增到 `3.2.45-Beta` 并替换本地 `publish/current`；不构成 GitHub 推送、GitHub Release 或 Microsoft Store 发布。
