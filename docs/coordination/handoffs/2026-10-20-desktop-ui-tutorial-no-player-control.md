# 教学模式无播放器控制按钮修复交接

## Task

- 任务线程：桌面端岛屿、设置与交互。
- 基线提交：`b287506c34c60b714914ace420aeed1b5a50336d`。
- 允许范围：`LyricHover.App/` 教学与播放控制 UI、`LyricHover.Tests/Program.cs` 回归覆盖。
- 问题：教学提示要求按住临时交互键并点击播放控制时，如果没有打开任何播放器，播放/暂停按钮因无媒体会话而禁用，教学无法继续。

## Result

- `IslandRenderState` 增加仅供教学控制练习使用的瞬时标记，不修改设置、Core 播放器或持久化契约。
- 只有教学流程处于 `AwaitingControlClick` 时，无媒体会话的播放/暂停按钮临时启用；上一首、下一首仍保持禁用。
- 点击后教学流程继续，播放命令在无会话时仍按原逻辑安全返回；离开练习步骤后按钮恢复普通的会话启用规则。
- 控件的未变化渲染缓存纳入教学标记，保证进入和退出练习步骤都会刷新启用状态。

## Changed Files

- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.App/Modules/IslandRenderState.cs`
- `LyricHover.App/Modules/PlaybackControlsModuleView.xaml.cs`
- `LyricHover.Tests/Program.cs`

## Verification

- `dotnet build LyricHover.sln -c Release --no-restore '/clp:ErrorsOnly;Summary'`：成功，0 错误、310 警告；警告为当前测试项目链接类型与 App 程序集同名等既有告警。
- 首次完整测试仅失败于发布版本夹具无法读取 `C:\Users\CodexSandboxOffline\.nuget\packages`，其余用例（包括新增教学用例）通过。
- 设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 后执行 `dotnet run --project LyricHover.Tests -c Release --no-build`：280 PASS、0 FAIL。
- `git diff --check`：通过，仅报告现有 LF/CRLF 转换提示。

## Manual Acceptance

尚未完成真实 Windows UI 验收。建议关闭所有播放器后重新开始教学，进入控制练习步骤，确认播放/暂停按钮不再灰显；按住设置的临时交互键点击后应进入下一步，上一首与下一首仍应灰显。

## Release State

- 已使用 `publish.ps1 -NoLaunch` 生成 v3.3.11，并替换 `publish/current`；旧 v3.3.10 已归档至 `publish/archive/v3.3.10`。
- 当前候选共 11 个文件、33,874,282 bytes；文件版本 `3.3.11.0`，产品版本 `3.3.11`。
- `LyricHover.App.dll` SHA-256：`6FCA8720D600BCD9C66B13F34238CF266A3CC2A3FFFFE0A91DDF3993C296D28F`。
- `LyricHover.Core.dll` SHA-256：`3E3C0D29BED039F21A9D126FBB4E0F6BBCAB73885E526E7AB2C86D5447ED998D`。
- App/Core DLL 均与本轮 `win-x64` Release 构建输出哈希一致；没有遗留 `staging-v3.3.11`，新版未自动启动。
- 未提交、未推送、未创建 GitHub Release、未提交 Microsoft Store。
