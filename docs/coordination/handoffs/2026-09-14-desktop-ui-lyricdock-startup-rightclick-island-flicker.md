# 任务交接：歌词坞启动、右键设置与自动折叠闪烁修复

- 日期：2026-09-14
- Owner：Desktop Island, Settings & Interaction UI
- 起始提交：`652f366aa1cce73ce14df4ee49e5d4625014f9b9`
- 工作分支：`codex/feature/desktop-fullscreen-auto-hide`
- 状态：实现、自动化验证及本地 `3.2.45-Beta` 候选打包完成；真实任务栏与动画肉眼验收待执行
- 允许范围：`LyricHover.App/`、相关 `LyricHover.Tests/` 回归与本交接

## Result

1. 歌词坞配置为开启、但启动时任务栏环境尚未准备好时，控制器保留用户启用意图，并由现有歌词刷新链以两秒节流自动重试，不再要求用户先关闭再开启。
2. 启动时旧 Widgets lease 首次恢复失败后，立即排队重试旧恢复记录；pending 期间仍禁止新 lease、Settings/UIA 自动化和恢复记录覆盖。
3. 歌词坞右键在原生右键按下时立即请求设置，同时覆盖 client/non-client/context-menu 与 WPF routed fallback；250 ms 去重避免一轮右键打开多次。
4. 移除“歌词坞已显示，但 Widgets 恢复尚未完成”的红字。真实环境不可用且歌词坞没有显示时，原有可操作失败提示继续保留。
5. 自动折叠的紧凑/展开布局切换时复用唯一同类型模块视图，尤其保持歌词模块的当前文本、逐字进度和动画状态，不再清空后重建造成一帧闪烁；分割线因自身样式由构造参数决定，仍按布局重建。

## Changed Files

- `LyricHover.App/LyricDock/LyricDockController.cs`
- `LyricHover.App/LyricDock/LyricDockWindow.cs`
- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.App/Modules/IslandModuleHost.xaml.cs`
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`
- `LyricHover.App/UiLanguageService.cs`
- `LyricHover.Tests/Program.cs`

其中 `MainWindow.xaml.cs`、设置和测试文件原先已有“全屏时自动隐藏歌词岛”WIP；本任务按行为增量修改，未覆盖或清理既有改动。

## Verification

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:ErrorsOnly
$env:LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE='1'
& .\LyricHover.Tests\bin\Release\netcoreapp3.1\LyricHover.Tests.exe
git diff --check
```

- Release 构建：退出码 0，0 error，253 warnings。
- 完整桌面回归：256 PASS、0 FAIL。
- 新增覆盖：启动 surface 自动重试、右键消息去重打开设置、紧凑/展开布局复用歌词模块，以及恢复红字不存在。
- `git diff --check`：通过，仅输出既有 CRLF 转换警告。
- Computer Use 成功启动本地 Release 进程，但透明非激活歌词窗口未暴露为可定位窗口，因此未完成真实 UI 输入。

## Local Candidate

- 路径：`D:\AppleMusicDesktopLyrics\publish\current`
- 版本：`3.2.45-Beta`
- 文件：11 个，共 33,817,842 bytes
- `LyricHover.App.exe` SHA-256：`C1A8336BB754CCF5C2CDCF2536926A76F240EE3B59F05300E9C1E02AE504D96B`
- `LyricHover.App.dll` SHA-256：`4BDA5B1AFE18EC5DA4C1D82D0A9CDDD0D1F67DA246850166C2FB3F8A863D2AE2`
- `LyricHover.Core.dll` SHA-256：`E01B53D465CBCFAB6C0CE1BBEF603EB5D221984F09F72B1A524E05C0419E863D`
- 发布脚本完整桌面回归：256 PASS、0 FAIL；独立版本事务夹具 PASS；win-x64 build/publish 为 0 warning、0 error。
- App/Core DLL 与最终 Release 输出一致；目标 staging 目录不存在；使用 `-KeepVersion -NoLaunch`，未自动启动候选。
- 被替换候选归档：`publish/archive/v3.2.45-Beta-20260914-210638`。

## Manual Acceptance Pending

1. 保持“开启歌词坞”为开，退出并重新启动数次；即使 Explorer/任务栏短暂较慢，也应自动显示，不需要重拨开关。
2. 在歌词坞文字范围内右键一次，设置窗口应只打开一次并聚焦“歌词坞”设置。
3. 开启歌词坞后，设置页不再显示 Widgets 恢复红字；若安全任务栏位置确实不可用，仍可显示环境不可用提示。
4. 使用“自动折叠”布局连续按住/松开临时交互快捷键，观察歌词、逐字高亮、封面和控制模块；伸展与收缩期间不得出现模块清空后闪回。
5. 验证任务栏自动隐藏、全屏、Explorer 重启及 Widgets 原状态 absent/0/1，确认旧 lease 仍精确恢复。

## Scope

- 未修改 Core 播放器、歌词解析、缓存或时间线语义。
- 未修改 Store 标识、单实例名称、数据目录迁移或发布脚本。
- 本地候选打包不等同于 GitHub Release、Microsoft Store 或 ESA 发布。
