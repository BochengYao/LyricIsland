# 发布交接：v2.1.9 Beta 设置页语言标识

- 日期：2026-08-11
- 发布基线：`d760bf8b4c4109d2e41c5a6e87cf2633108bd235`
- 功能交接：[设置页语言标识](2026-08-11-desktop-island-settings-ui-language-symbol.md)
- 版本：`v2.1.9 Beta`；MSIX 版本：`2.1.9.0`

## 纳入与排除

- 纳入：设置页可见语言标签改为 `文 / A`、保留本地化 Tooltip 和读屏名称、四种语言的通用映射及回归断言。
- 排除：共享工作树中的 ComboBox 选择内容模板改动、所有 Core 歌词改动、本地资产、其他未提交交接文件。

## 本地构建与产物（已完成）

- `dotnet restore LyricHover.sln` 与 win-x64 运行时还原：通过。
- `publish.ps1 -NoLaunch`：通过；完整桌面回归通过，Release 构建为 0 警告、0 错误。
- `store/msix/build-msix.ps1 -SkipTests -KeepStaging`：通过；MSIX 已解包复核。
- `publish/current/LyricHover.App.exe`：`2.1.9-Beta`，文件版本 `2.1.9.0`。
- `publish/current/LyricHover.App.dll` SHA-256：`9B4260023D556669FE4C7FBC3236558CB086EEA14D2E67A8EC6E825BC9B09081`。
- `store/package/msix/LyricHover_2.1.9.0_x64.msix`：87,816,996 bytes，SHA-256 `0B0FF9465E5BFC3DDF82315BEF27B45DEAC8A8095C27B8F605CB9BE70F1FC85E`。
- 解包身份：`70643607.LyricIsland`、`CN=D0EA2A8A-59FF-4BC5-AB6E-5ABC356AF3E3`、`2.1.9.0`、`x64`；PDB 数量为 0；解包 DLL SHA-256 与 `publish/current` 一致。
- MSIX 签名：未签名；由 Microsoft Store 接收后签名。
- 根目录 `publish/current` 已提升为 v2.1.9-Beta；原 `publish/current` 已精确归档为 `publish/archive/v2.1.8-Beta`。

## 外部状态

- GitHub：提交 `c49e8bd692c98789be77559b91b3578aa3a8232a` 已推送至 `origin/codex/release/v2.1.9-language-symbol`；未合并 `main`，未创建 GitHub Release。
- Microsoft Store：未选择包、未上传、未平台验证、未提交审核、未发布。
- 官网：本次未包含 `website/` 文件或 ESA 部署，未执行官网发布。

## 回滚

- 本机上一版 `v2.1.8-Beta` 在提升 `publish/current` 时保留为精确归档；回滚应只恢复该归档及对应 MSIX，不能删除整个 `publish/archive`。
