# Desktop Core：QRC 引号截断修复

- 日期：2026-09-10
- Owner：Desktop Lyrics & Player Core
- 基线：`210ae8e4e9fd9f39cc2b89c246eacd8850d37d6c`
- 分支：`codex/fix-qrc-quoted-content`
- 状态：已集成 `main` 并生成 `v3.2.41 Beta` 本地候选；真实 UI 动画验收待完成

## 问题证据

QRC 解密结果使用类似 XML 的 `LyricContent` 属性承载正文，但 QQ 返回内容可能在歌词正文中直接包含英文双引号。旧正则把正文中的第一个双引号误判为属性结束：`Opalite` 原文逐字轨被截断在 14.349 秒，`Elizabeth Taylor` 被截断在约 35 秒，随后覆盖校验只能安全降级为完整逐行歌词。

## 修复

- QRC 正文提取改为以 `<Lyric_1 ... LyricContent="..."/>` 元素结束标记为边界，不再以正文中的首个双引号为边界。
- 保留 HTML 实体解码和“非 QRC XML 时返回原文”的既有降级行为。
- 新增回归测试，验证带双引号的歌词以及引号后的下一句均能保留并解析逐字时间。

## 验证

- `dotnet build LyricHover.sln -c Release`：成功，0 error；245 个既有测试工程类型冲突/未使用事件警告。
- `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1 dotnet run --no-restore --configuration Release --project LyricHover.Tests`：251 项测试全部通过，包含新增 `preserves quoted text in decrypted QQ QRC`。
- `dotnet run --project LyricHover.Core.TranslationContractTests -c Release`：通过。
- 真实 QQ 在线探针：`Opalite` 返回 66 行、62 行含逐字时间、末行 228.613 秒；`Elizabeth Taylor` 返回 68 行、64 行含逐字时间、末行 201.474 秒；`Roar` 保持 70/70 行逐字时间、末行 216.343 秒。
- `git diff --check`：通过。

## 边界与后续验收

- 未修改 UI、设置、播放器时间线或用户歌词缓存。
- 普通 LRC、无法解密的 QRC 和真正残缺的逐字包继续沿用安全降级。
- 集成候选应强制刷新上述两首歌曲，确认本机旧的逐行缓存被新 QRC 替换，并完成真实 UI 逐字动画验收。
- `publish/current` 已生成 `v3.2.41 Beta`；本交接不代表 GitHub 推送、GitHub Release、Microsoft Store 上传或正式发布。
