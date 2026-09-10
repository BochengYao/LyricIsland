# 任务交接：重复启动时临时唤出已缩回的歌词岛

- 日期：2026-09-10。
- Owner：Desktop Island, Settings & Interaction UI。
- 起始基线：`77cd50f`（`main` / `origin/main`）。
- 版本：`3.2.43-Beta`。
- 状态：实现、自动化验证、本地候选打包完成；未推送 GitHub、未提交 Store、未正式发布。
- 允许范围：`LyricHover.App/`、相关 `LyricHover.Tests/` 回归与本交接。

## Result

已有实例收到第二次启动的激活信号时，只在歌词岛开关开启且岛体已经缩回时，将岛体临时弹出 30 秒并重新开始一次性倒计时。岛体已经显示或开关关闭时不执行任何显示或重置；首次启动仍使用用户配置的无播放自动收起时间。

若临时弹出期间开始正常播放，则退出临时倒计时并继续按播放可见性规则显示。30 秒到期时，若仍无播放或不在播放状态，则重新缩回。

## Changed Files

- `LyricHover.App/App.xaml.cs`：将单实例激活事件路由到重复启动专用入口。
- `LyricHover.App/MainWindow.xaml.cs`：增加开关/缩回状态门控、30 秒一次性计时与运行停止清理。
- `LyricHover.Tests/Program.cs`：增加重复启动行为回归约束。

工作区在本任务前已有 `3.2.42`、逐字跟随命名及相关测试/文档改动；该功能已由独立提交 `bc5ed9d` 纳入。本任务没有覆盖或回退该提交。

## Verification

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:ErrorsOnly
$env:LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE='1'
& .\LyricHover.Tests\bin\Release\netcoreapp3.1\LyricHover.Tests.exe
```

- 发布脚本：`publish.ps1 -NoLaunch` 首轮在更新 `publish/README.md` 时因沙箱权限失败；产物已替换但版本源回滚。恢复版本源后在获准的沙箱外环境执行 `publish.ps1 -KeepVersion -NoLaunch` 成功，最终输出 `发布完成：v3.2.43 Beta`。
- 发布脚本内桌面回归：247 PASS、0 FAIL。
- win-x64 framework-dependent Release 构建与 publish：0 error、0 warning。
- 版本事务夹具：设置 `NUGET_PACKAGES=C:\Users\14731\.nuget\packages` 后 PASS；未设置时提权会话错误定位到不存在的沙箱用户缓存，该次失败未进入事务断言。
- 未执行会干扰当前常驻实例的双击实机验证。实机验收应覆盖：开关关闭、已展开、无播放缩回、暂停缩回、30 秒内开始播放五种状态。

## Candidate

- 路径：`D:\AppleMusicDesktopLyrics\publish\current`。
- 文件：11 个，共 33,809,206 bytes。
- `LyricHover.App.exe`：ProductVersion `3.2.43-Beta`，FileVersion `3.2.43.0`，SHA-256 `D643B8BD12D48BF9DCE064E9E67C4DF7CA40AC39770725F1DD79D737863C0F92`。
- `LyricHover.App.dll`：ProductVersion `3.2.43-Beta`，FileVersion `3.2.43.0`，SHA-256 `295124AB1B204C597BF3F7430C68055C2CC4C724CE03FF9C9764849ECB005EC4`。
- `LyricHover.Core.dll`：SHA-256 `10198FB95FE253CD79D073B9B18F4499E910BFE5B6A0077A929EA5045DDD55EE`。
- 原 `3.2.42-Beta` current 已归档至 `publish/archive/v3.2.42-Beta`；事务恢复过程中产生的首轮 `3.2.43-Beta` 中间候选保留在同名归档，未覆盖历史产物。
- 使用 `-NoLaunch`，最终候选未自动启动。

## Scope and Impact

- 未修改 `LyricsIsland.DesktopLyrics.SingleInstance` 兼容名称或 `SingleInstanceGuard` 协议。
- 未修改设置键、默认值、Core 播放/歌词/缓存语义。
- 当前只证明本地候选打包完成，不构成 GitHub Release、Store 提交或正式发布证明。
