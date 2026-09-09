# Desktop Core：残缺逐字歌词覆盖修复

- 日期：2026-09-09
- Owner：Desktop Lyrics & Player Core
- 基线：`82e921a85182579367bf36ddf25fd2d2b10a9f72`
- 分支：`codex/fix-word-timed-coverage`
- 状态：已集成 `main` 并生成 `v3.2.40 Beta` 本地候选，真实播放器验收待完成

## 问题证据

本机《Opalite》缓存的原文逐字轨最后一条有文字的时间戳为 14.349 秒，翻译轨则延伸至 3:48.61。旧校验只要求至少存在一条有效原文，因此该残缺包被接受并缓存；播放越过 14.349 秒后，选句器持续返回最后一条原文，而媒体进度继续前进。

## 修复

- 新增 Core 覆盖一致性校验。只有同一包翻译轨或已验证且同源的普通歌词轨提供至少三条明确后续行，并同时满足尾部相差至少 30 秒、逐字原文覆盖不足参考轨 75% 时，才判定逐字原文被截断。
- 缓存验收复用该校验，旧的残缺逐字缓存会成为 cache miss 并触发重新获取。
- 新获取的残缺逐字包不再覆盖完整普通歌词；保留普通歌词作为合法降级结果。
- 普通参考轨必须与逐字轨至少两句规范化文本和时间匹配，避免不同歌词版本仅因时间更长而误触发降级。

## 验证

- `dotnet build LyricHover.sln -c Release --no-restore`：成功，0 error；245 个既有测试工程类型冲突警告。
- `dotnet run --project LyricHover.Tests -c Release --no-build`：250 PASS、0 FAIL，其中新增 3 项覆盖残缺缓存、完整 LRC 回退与稀疏尾注释反例。
- `dotnet run --project LyricHover.Core.TranslationContractTests -c Release`：通过。
- `git diff --check`：通过。

## 边界与后续验收

- 功能提交未修改用户缓存、设置或 UI；后续发布集成将版本推进到 `3.2.40-Beta`，并生成 `publish/current` 本地候选。
- 集成候选需真实播放《Opalite》并强制刷新，确认旧缓存被拒绝、歌词在 14 秒后继续换句；同时抽测一首正常 QQ/网易逐字歌词与一首长尾奏歌曲。
- 本交接不代表已创建 GitHub Release、上传或提交 Microsoft Store，也不代表 Store 正式发布。
