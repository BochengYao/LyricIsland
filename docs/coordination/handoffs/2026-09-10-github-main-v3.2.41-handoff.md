# 任务交接：v3.2.41 Beta QRC 修复 GitHub 主线同步

- 日期：2026-09-10
- 来源任务：排查歌词逐字效果缺失
- 交接目标：Desktop Core、Desktop UI & Interaction、Quality & Release
- 推送前远端：`210ae8e4e9fd9f39cc2b89c246eacd8850d37d6c`
- 功能提交：`529735991eda02e1db4c162e2ac4d78d57fa4127`
- 发布提交：`2f4146f1d4b95d2c5740554250f0886d84f808b5`
- 目标远端：`origin/main`（`https://github.com/BochengYao/LyricIsland.git`）

## 已完成

- 用户明确授权后，将 QRC 双引号截断修复与 `v3.2.41 Beta` 发布记录推送到 GitHub `main`。
- 修复保持在 `LyricHover.Core` 解密正文提取边界内，未改变 UI、设置、播放器时间线或缓存格式。
- `publish/current` 保持为本机框架依赖候选；GitHub 源码同步不等于 GitHub Release、Microsoft Store 上传、提交审核或正式发布。

## 交接路由

### Desktop Core

- 接收功能提交 `5297359`，维护 QRC 正文包含英文双引号时不得截断的解析约束。
- 保持 `LyricLine.Words` 为可选增强；QRC 无效或覆盖不完整时继续保留完整逐行歌词回退。

### Desktop UI & Interaction

- 使用 `publish/current` 的 `3.2.41-Beta` 对 `Opalite`、`Elizabeth Taylor` 执行强制刷新与整首视觉验收。
- 覆盖逐字连续推进、暂停/恢复、跳转和切歌；UI 不新增第二时间线或自行解析 QRC。

### Quality & Release

- 接收发布提交 `2f4146f` 与本地候选哈希，核验远端提交、候选版本和文件一致性。
- 保持渠道状态分离：当前仅 GitHub 源码已推送；GitHub Release、MSIX、Store 均未执行。

## 验证证据

- 桌面回归：251 项全部通过。
- Core translation contract tests：通过。
- 真实 QQ 在线探针：`Opalite` 66 行/62 行逐字，`Elizabeth Taylor` 68 行/64 行逐字，`Roar` 70 行/70 行逐字。
- `publish/current`：ProductVersion `3.2.41-Beta`，共 11 个文件、33,808,494 bytes。
- `LyricHover.App.exe` SHA-256：`9E88505CD5809A81AE23B05006F2A2CFAD09FF85E13DE0FF529669A68221D32E`。
- `LyricHover.App.dll` SHA-256：`CE861E0DA2810D3DD1A489DE885D2880105DE6C5DD0BFEB03ACA0C502BD21693`。
- `LyricHover.Core.dll` SHA-256：`098C04EFE0B28733EE52E6622584C2D82D9B4FDABE591D4FA53EDDEFEB2F95B1`。

## 未修改 / 非目标

- 未纳入主工作树既存的 `docs/desktop-core-domain-knowledge.md` 与托盘菜单 closeout WIP。
- 未创建标签或 GitHub Release，未上传二进制附件。
- 未生成、上传或提交 Microsoft Store 包。

## 风险与后续

- 已知限制：自动化和真实 QQ 数据获取已验证，但尚缺运行中歌词岛的全曲逐字动画验收。
- 回滚点：功能前基线 `210ae8e4e9fd9f39cc2b89c246eacd8850d37d6c`；前一候选为 `publish/archive/v3.2.40-Beta/`。
