# 任务交接：任务栏右键菜单期间保持歌词坞位置

- 日期：2026-09-16
- Owner：Desktop Island, Settings & Interaction UI
- 起始提交：`eb9e859`
- 功能与候选提交：`036289326ee66efdc03d8ed35d33898d89e8d037`
- 工作分支：`codex/feature/desktop-fullscreen-auto-hide`
- 状态：实现、自动化验证、本地 `3.2.48-Beta` 候选打包和本地 Git 提交完成；真实 Windows 任务栏右键目测待执行
- 允许范围：`LyricHover.App/LyricDock/`、相关 `LyricHover.Tests/` 回归与本交接

## Goal

修复右键任务栏打开 Shell 上下文菜单时，歌词坞因瞬态不完整的任务栏 UIA 布局被重算到最左端，而不是继续位于最后一个任务栏图标后的问题。

## Boundaries

- 不修改 Core 播放器、歌词解析、缓存或时间线语义。
- 不修改 Widgets 租约、设置键、显示屏选择或任务栏探测节拍。
- 保留图标真实增删后的正常重新定位；只隔离任务栏上下文菜单存续期间的不稳定探测结果。

## Verification Plan

1. 增加位置稳定策略回归，覆盖菜单打开、菜单关闭、任务栏边界变化和无历史位置。
2. 运行 Release 构建与完整桌面测试。
3. 实机验证右键任务栏时位置不跳到最左端，关闭菜单后仍可跟随真实图标变化。

## Root Cause

`WindowsLyricDockEnvironment.TryGetPlacement()` 会把每次任务栏 UIA 扫描得到的最大安全间隙立即作为新位置。任务栏右键菜单打开期间，Explorer 的 UIA 子树可能只暴露部分任务栏控件；这次瞬态快照仍能通过“有安全间隙”的校验，于是原本从最后一个图标之后开始的间隙被误判为从任务栏最左端开始，控制器随后按正常位置变化执行了横向缓动。

## Result

1. `WindowsLyricDockEnvironment` 按显示器缓存最近一次完整、成功的任务栏位置。
2. 当任务栏线程处于原生菜单模式，或检测到与任务栏相关且紧邻任务栏的菜单/Popup 窗口时，如果任务栏边界未变，直接复用最近一次位置，不消费菜单期间的不完整 UIA 快照。
3. 菜单关闭后，下一次普通探测继续扫描真实任务栏布局，因此图标增删、DPI/显示器变化和既有 260 ms 横向缓动仍按原逻辑工作。
4. 没有历史成功位置或任务栏边界发生变化时不复用缓存，继续走原有安全探测；全屏、自动隐藏和 Widgets 租约语义未改变。

## Changed Files

- `LyricHover.App/LyricDock/ILyricDockEnvironment.cs`：新增可测试的位置稳定策略和原生菜单模式标志判断。
- `LyricHover.App/LyricDock/WindowsLyricDockEnvironment.cs`：缓存成功位置，识别 Win32/XAML 任务栏菜单并在其存续期间复用缓存。
- `LyricHover.Tests/Program.cs`：覆盖位置复用、菜单标志、菜单关闭、显示器边界变化和无历史位置。
- `Directory.Build.props`：本地候选版本推进至 `3.2.48-Beta`。
- `CHANGELOG.md`：补充面向用户的任务栏右键位置修复说明。

## Verification

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet build LyricHover.App\LyricHover.App.csproj -c Release --no-restore -t:Rebuild -v:minimal
$env:LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE='1'
& .\LyricHover.Tests\bin\Release\netcoreapp3.1\LyricHover.Tests.exe
git diff --check -- LyricHover.App/LyricDock/ILyricDockEnvironment.cs LyricHover.App/LyricDock/WindowsLyricDockEnvironment.cs LyricHover.Tests/Program.cs docs/coordination/handoffs/2026-09-16-desktop-ui-lyricdock-taskbar-context-menu-placement.md
```

- App Release Rebuild：0 warning、0 error。
- 完整桌面回归：267 PASS、0 FAIL。
- `git diff --check`：通过，仅有工作区既有 LF→CRLF 提示。
- 解决方案构建的测试项目仍会输出已登记的源码链接 `CS0436` 警告；正式 App 项目没有警告。

## Manual Acceptance Pending

1. 保持歌词坞位于最后一个左对齐任务栏图标后，右键任务栏空白处并让菜单停留至少 5 秒；歌词坞不得移动到最左端。
2. 关闭菜单后打开/关闭一个会改变任务栏图标数量的应用；歌词坞应继续以既有缓动跟随最后一个图标。
3. 覆盖 100%/125%/150% DPI、主副屏、任务栏自动隐藏、全屏和 Explorer 重启；确认没有错误复用跨屏或旧边界位置。

## Git / Release State

- 功能、版本源、更新日志与初版交接已提交为 `036289326ee66efdc03d8ed35d33898d89e8d037`；本文件的最终交接补充由其后的 `docs(handoff)` 提交记录。
- 当前分支仅本地提交，尚未推送 GitHub；接收线程不可把“消息已送达”当作已合并或已发布。
- `publish.ps1 -NoLaunch` 已生成并替换 `publish/current`：`3.2.48-Beta`，8 个文件，共 24,576,663 bytes。
- App EXE SHA-256：`31DD553FFA252A5CAFF0D40AEA535C2D041FE5D599CCCBFC2D49C20F68834739`。
- App DLL SHA-256：`ED1990E5A46A4328B878AE915F996F9D418B9F34FF4EA66ECE89D5ED22DC934C`。
- Core DLL SHA-256：`5F84A24DC97DC15326689BD103107BF13C226543EB2E40B2296E06A89A482CE6`。
- App/Core DLL 与同次 Release 输出哈希一致；`publish/staging-v3.2.48-Beta` 已清理。
- 旧候选归档：`publish/archive/v3.2.47-Beta-20260920-113437`。
- 未创建 GitHub Release、标签或附件；未上传或提交 Microsoft Store/ESA。
- 网站、宣传图和 `output/canva-assets` 等其他线程 WIP 保持未暂存、未修改。
