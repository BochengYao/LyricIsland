# 任务交接：歌词坞跟随任务栏隐藏与恢复

- 日期：2026-09-14
- Owner：Desktop Island, Settings & Interaction UI
- 起始提交：`fba162b`
- 功能提交：`9f9d4e87b0c51395d296f5c4cd1f00ba9389a3b2`
- 工作分支：`codex/feature/desktop-fullscreen-auto-hide`
- 状态：实现、自动化验证、本地提交与 `publish/current` 打包完成；真实任务栏/全屏视觉验收待执行
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
$env:NUGET_PACKAGES='C:\Users\14731\.nuget\packages'
dotnet run --no-restore --configuration Release --project LyricHover.Tests\LyricHover.Tests.csproj -- --release-version-fixture
& .\publish.ps1 -KeepVersion -NoLaunch
git diff --check
```

- 独立版本事务夹具：PASS。
- 发布脚本完整桌面回归：257 PASS、0 FAIL。
- win-x64 Release build/publish：0 warning、0 error。
- `git diff --check`：通过，仅输出既有 LF/CRLF 转换提示。
- 打包前停止了锁住标准 Release 输出的旧 `LyricHover.App.exe`；使用 `-NoLaunch`，未自动启动最终候选。

## Local Candidate

- 路径：`D:\AppleMusicDesktopLyrics\publish\current`
- 版本：`3.2.45-Beta`；FileVersion `3.2.45.0`
- 文件：11 个，共 33,817,846 bytes
- `LyricHover.App.exe` SHA-256：`C1A8336BB754CCF5C2CDCF2536926A76F240EE3B59F05300E9C1E02AE504D96B`
- `LyricHover.App.dll` SHA-256：`11BEF3B022CEC85AB8EE800E8CA5447D0FD9B1B3A78FA480995495E49FF4B146`
- `LyricHover.Core.dll` SHA-256：`E01B53D465CBCFAB6C0CE1BBEF603EB5D221984F09F72B1A524E05C0419E863D`
- App/Core DLL 与最终 Release 输出哈希一致；精确 `staging-v3.2.45-Beta` 不存在。
- 被替换候选归档：`publish/archive/v3.2.45-Beta-20260914-220648`。
- 既有 `publish/staging-diagnose-v3.1.39-Beta` 不属于本次打包，保持不动。

## Manual Acceptance Pending

1. 开启歌词坞，保持歌词岛的“全屏时自动隐藏”关闭，进入浏览器或播放器全屏；任务栏隐藏后歌词坞应在约 300 ms 内隐藏。
2. 退出全屏；任务栏恢复后歌词坞应自动恢复，无需重拨开关。
3. 在等待播放、暂停和播放中分别重复；行为应一致。
4. 验证 Windows 任务栏自动隐藏、主副屏和省电模式；省电模式下监视间隔为约 1 s。

## Scope

- 未修改 `LyricHover.Core/`、播放器、歌词、缓存或时间线语义。
- 未修改设置键、默认值、Store 标识、单实例名称或数据目录迁移。
- 未创建 GitHub Release、标签或附件；未上传、验证或提交 Microsoft Store、ESA。
