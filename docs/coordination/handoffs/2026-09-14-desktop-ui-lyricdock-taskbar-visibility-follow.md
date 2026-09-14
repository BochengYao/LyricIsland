# 任务交接：歌词坞跟随任务栏隐藏与恢复

- 日期：2026-09-14
- Owner：Desktop Island, Settings & Interaction UI
- 起始提交：`fba162b`
- 工作分支：`codex/feature/desktop-fullscreen-auto-hide`
- 状态：实现与自动化验证完成；真实任务栏/全屏视觉验收待执行
- 允许范围：`LyricHover.App/`、相关 `LyricHover.Tests/` 回归与本交接

## Result

歌词坞现在通过现有 300 ms 全屏监视节拍独立复查任务栏可见性，不再依赖歌词内容刷新。任务栏因全屏或自动隐藏而不可用时，歌词坞临时隐藏；任务栏恢复后自动重新显示。

这次变化只抑制运行时表面可见性：控制器继续保留歌词坞启用意图，不持久化关闭设置，也不因临时隐藏恢复或重建 Widgets 租约。歌词岛的“全屏时自动隐藏”开关、默认值与语义不变。

## Root Cause

`LyricDockController.RefreshPlacement()` 已能处理 `TaskbarAutoHiddenOrFullscreen`，但此前主要由歌词快照刷新触发。等待播放、暂停或其他没有生成新歌词快照的路径不会持续重新探测任务栏；同时，`fullscreenMonitorTimer` 仅在歌词岛的全屏隐藏开关开启时运行。因此任务栏进入隐藏状态后，歌词坞可能继续显示。

## Changed Files

- `LyricHover.App/MainWindow.xaml.cs`：歌词坞开启时保持现有全屏监视器运行，并在每次监视节拍刷新歌词坞位置/可见性。
- `LyricHover.Tests/Program.cs`：覆盖无歌词刷新时的隐藏与恢复，并断言临时隐藏不关闭控制器启用状态。

## Verification

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:ErrorsOnly /p:OutDir='D:\AppleMusicDesktopLyrics\.tmp\lyricdock-visibility-build\'
$env:LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE='1'
& 'D:\AppleMusicDesktopLyrics\.tmp\lyricdock-visibility-build\LyricHover.Tests.exe'
git diff --check
```

- 隔离 Release 构建：退出码 0，0 error，259 warnings。
- 完整桌面回归：退出码 0，全部 PASS、0 FAIL。
- `git diff --check`：通过，仅输出既有 LF/CRLF 转换提示。
- 标准输出目录的首次构建被正在运行的 `LyricHover.App.exe` 文件锁阻挡；未终止用户当前实例，改用隔离 `OutDir` 验证同一 Release 源码。

## Manual Acceptance Pending

1. 开启歌词坞，保持歌词岛的“全屏时自动隐藏”关闭，进入浏览器或播放器全屏；任务栏隐藏后歌词坞应在约 300 ms 内隐藏。
2. 退出全屏；任务栏恢复后歌词坞应自动恢复，无需重拨开关。
3. 在等待播放、暂停和播放中分别重复；行为应一致。
4. 验证 Windows 任务栏自动隐藏、主副屏和省电模式；省电模式下监视间隔为约 1 s。

## Scope

- 未修改 `LyricHover.Core/`、播放器、歌词、缓存或时间线语义。
- 未修改设置键、默认值、Store 标识、单实例名称或数据目录迁移。
- 未打包、提交、推送或发布外部候选。
