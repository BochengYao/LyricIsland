# 任务交接：V3 版本基线与候选生成互斥

- 日期：2026-08-17
- 任务线程：06-release-github-store
- 基线提交：`ece2c8c6b002c438b3665c34b93ee77134dad32e`（直接父提交 `6941e86d1278e77c4898bddbc8e3344355fea0dc`）
- 结果提交：本交接所在的本地 V3 基线提交（最终 SHA 由发布线程回传；本阶段不推送）
- 允许修改范围：`Directory.Build.props`、`tools/publish-next-version.ps1`、`LyricHover.Tests/Program.cs`、本交接。

## 已完成

- 唯一版本源改为 `3.0.0-Beta`；程序集和文件版本由同一 `VersionPrefix` 派生为 `3.0.0.0`，About 保持显示为 `v3.0.0 Beta`。
- 候选生成脚本在读取 `Directory.Build.props` 前获取进程间命名互斥锁；并发调用失败时不会读取或递增版本。
- 非 `-KeepVersion` 成功完成时保留补丁递增；任一失败恢复原始版本文件。`-KeepVersion` 先验证 `publish/current/LyricHover.App.dll` 已存在且四段文件版本属于当前版本，缺失或不匹配即失败且不生成候选。
- 自动版本测试使用完整的临时假工程调用同一生产脚本，并在结束后删除临时目录；覆盖 `3.0.0 → 3.0.1`、构建失败回滚、两个并发调用仅一个递增、已有候选 `-KeepVersion` 复现及无候选拒绝、About/程序集文件/MSIX 四段映射。
- Store 身份、应用 ID、单实例名和旧数据目录迁移路径均只读核验，未修改。

## 未修改 / 非目标

- 未修改 `LyricHover.App` 业务 UI、Core 业务逻辑、Store 清单、应用 ID、单实例名或用户数据迁移逻辑。
- 未运行仓库的 `tools/publish-next-version.ps1` 以生成候选；未写入仓库 `publish/current` 或 `publish/archive`。
- 未生成 MSIX、未上传、未推送 GitHub、未触发 ESA、未创建 Store 提交或发布。

## 验证

- 命令：`dotnet build LyricHover.sln -c Release`（使用 `TargetPlatformSdkPath`/`TargetPlatformDisplayName`）。
- 结果：通过，0 警告、0 错误；产物 `LyricHover.Core.dll` 与 `LyricHover.App.dll` 的 FileVersion 均为 `3.0.0.0`，ProductVersion 均为 `3.0.0-Beta`。
- 命令：`dotnet run --project LyricHover.Tests -c Release --no-build`（同一 Release 构建）。
- 结果：新增版本测试全部通过；完整套件仅保留既有两项失败：`translation mode explains why single line is unavailable`、`mouse avoidance settings fit without scrolling`。两项均为 `docs/testing/REGRESSION_BASELINE.md` 登记基线，未为绿灯修改无关业务。
- 命令：`git diff --check`。
- 结果：通过。
- 身份核验：MSIX 仍为 `70643607.LyricIsland` / `LyricsIsland` / `LyricHover.App.exe`；单实例名仍为 `LyricsIsland.DesktopLyrics.SingleInstance`；迁移仍保留 `LyricsIsland` 与 `AppleMusicDesktopLyrics` 旧目录。

## 风险与后续

- 已知限制：本阶段只完成 V3 开发基线和本地验证；没有真实 MSIX 构建或 Store 产物，MSIX 仅验证版本源到 `3.0.0.0` 的构建脚本映射。
- 交接目标：任务栏歌词线程可从本地 V3 基线继续；仅在规定门禁通过后由发布线程执行正常候选生成，首个候选应为 `v3.0.1 Beta`。
- 回滚点：`ece2c8c6b002c438b3665c34b93ee77134dad32e`；不影响根工作树或远端 `main`。
