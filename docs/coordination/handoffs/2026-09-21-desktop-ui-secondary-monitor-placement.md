# 第二显示器歌词坞与设置窗口定位修复

## Task

- 修复歌词坞在第二显示器左下任务栏区域出现坐标偏移的问题。
- 修复从第二显示器歌词坞右键打开设置时，设置窗口落到主显示器的问题。

## Baseline and ownership

- 基线提交：`85abf8a92fa9fbb0eb39280a15e22a010e589939`（v3.2.48 桌面候选线）
- 分支：`codex/feature/desktop-multimonitor-placement-v3248`
- 允许修改：`LyricHover.App/` 窗口定位、`LyricHover.Tests/` 回归覆盖、本交接。
- 明确不修改：Core、播放器、歌词解析/缓存/时间线、Widgets 租约、设置 schema、网站和宣传素材 WIP。

## Result

- 歌词坞不再把 Win32 返回的绝对物理像素坐标整体除以目标屏 DPI；只换算相对目标显示器原点的偏移，保留第二屏原点并继续沿用现有 260 ms 横向缓动。
- 设置窗口在 HWND 初始化后按 `ScreenName` 对应显示器的工作区居中；完成 DPI 切换后再居中一次，已打开的设置窗口也会在歌词坞再次请求时移到目标屏。
- 未修改设置 schema、默认值和持久化格式。

## Verification

- `dotnet build LyricHover.sln -c Release`：在 v3.2.48 基线通过，0 error；测试工程报告 310 个既有源链接类型冲突/未使用测试事件警告。
- `dotnet run --project LyricHover.Tests -c Release --no-build`：退出码 0，完整 271 项回归全部 PASS；新增用例覆盖右侧屏物理原点、左侧负坐标屏设置窗口居中及生产调用路径。
- `publish.ps1 -KeepVersion -NoLaunch`：首次因 win-x64 运行时资产缺失报 `NETSDK1047`；执行 `dotnet restore LyricHover.App/LyricHover.App.csproj --runtime win-x64` 后重跑成功，Release 构建 0 warning / 0 error，并原子替换 `publish/current`。
- `publish/current`：`3.2.48-Beta`，11 个文件、33,833,766 bytes；`LyricHover.App.dll` SHA-256 为 `47B3208CD01D381D76680B27BBE31E36EA44F9D8634C753B369ECFEE036E9A75`，`LyricHover.Core.dll` SHA-256 为 `5F84A24DC97DC15326689BD103107BF13C226543EB2E40B2296E06A89A482CE6`。
- 原 `publish/current` 已归档到 `publish/archive/v3.2.48-Beta`；发布脚本停止了从旧 `publish/current` 运行的进程，且因 `-NoLaunch` 未重新启动应用。仓库内既有 `publish/staging-diagnose-v3.1.39-Beta` 为 2026-08-24 遗留诊断目录，不属于本轮发布，未改动。
- 当前主机检测到真实第二显示器 `\\.\DISPLAY31`，位于主屏左侧，且现有设置目标正是该屏；为避免改写 Widgets/设置，本次未启动新构建执行交互验收。
- 仍需实机确认：第二屏歌词坞左下位置、混合 DPI、第二屏右键打开设置窗口、第二屏位于主屏左右两侧。

## Delivery boundary

- 本交接随修复提交到 `codex/feature/desktop-multimonitor-placement-v3248`，最终提交 SHA 以 Git/GitHub 交付回执为准。
- `publish/current` 是本机候选目录，不纳入 Git；本轮只推送修复分支，不创建 GitHub Release、不提交 Microsoft Store。
