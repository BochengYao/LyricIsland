# 发布交接：v2.1.8 Core 歌词译文与本地化曲名恢复候选

- 日期：2026-08-11
- 基线提交：`3782e6e723368c9456091b0c12a2ded1a99655b7`
- 版本：`2.1.8-Beta`（MSIX：`2.1.8.0`）
- 纳入交接：[Core 歌词译文对齐与本地化曲名检索恢复](2026-08-11-desktop-lyrics-player-core-translation-alias-recovery.md)

## 隔离与构建

- 使用独立工作树 `D:\AppleMusicDesktopLyrics\.worktrees\release-v218-core`，仅纳入 `LyricHover.Core/` 与 `LyricHover.Core.TranslationContractTests/` 的 6 个已验收文件。
- 未纳入根工作树未验收的 `LyricHover.App/PlacementSettingsWindow.xaml` 或关联的 `LyricHover.Tests/Program.cs` 修改。
- 执行 `dotnet restore LyricHover.sln`、`dotnet restore --runtime win-x64 LyricHover.App\LyricHover.App.csproj`、`publish.ps1 -NoLaunch`、`store\msix\build-msix.ps1 -SkipTests -KeepStaging`。
- 独立 Core 契约项目不在解决方案默认构建产物中，故另行执行 `dotnet build LyricHover.Core.TranslationContractTests\LyricHover.Core.TranslationContractTests.csproj -c Release` 后再以 `--no-build` 运行。

## 验证

- Core 契约：通过，`Core translation contract tests passed.`，0 警告、0 错误。
- 完整桌面回归：通过；已登记的两项基线也通过，没有新增失败。
- `publish/current/LyricHover.App.exe`：产品版本 `2.1.8-Beta`，文件版本 `2.1.8.0`。
- 当前 DLL SHA-256：`ABB65AC27A895EAAF1137F6D643540B32D4CAE2E358B5B6A73E9947FEF2F2AA1`。
- MSIX：`store/package/msix/LyricHover_2.1.8.0_x64.msix`，87,816,754 bytes，SHA-256 `9E645D26F6D3C91098A2CB27C51AEB089C5FEA13AD677EA4E255A51809909CD3`。
- 解包身份：`70643607.LyricIsland` / `CN=D0EA2A8A-59FF-4BC5-AB6E-5ABC356AF3E3` / `2.1.8.0` / `x64`；解包 DLL 哈希与 `publish/current` 一致；MSIX 内无 PDB。

## 产物与外部状态

- 已生成并提升：`publish/current`。
- 已归档：上一份 `publish/current` 至 `publish/archive/v2.1.7-Beta`。
- 已生成：本地未签名 MSIX；签名状态 `NotSigned`，等待 Microsoft Store 处理。
- 未执行：Git 提交、GitHub 推送或 Release、Partner Center 上传、提交审核、Store 发布、官网生产发布。

## 回滚

- 恢复 `publish/archive/v2.1.7-Beta` 为 `publish/current`，并移除本地 `2.1.8` MSIX；不得递归删除归档目录。
