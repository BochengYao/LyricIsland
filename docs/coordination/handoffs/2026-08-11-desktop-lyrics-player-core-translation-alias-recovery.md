# 任务交接：歌词译文对齐与本地化曲名检索恢复

- 日期：2026-08-11
- 任务线程：desktop-lyrics-player-core
- 基线提交：`3782e6e723368c9456091b0c12a2ded1a99655b7`
- 结果提交：未提交（工作树交接）
- 允许修改范围：`LyricHover.Core/`、核心契约测试与本交接文档。

## 已完成

- 翻译显示改为按当前原歌词寻找最近的译词，容忍来源轨道最多 1.5 秒的小幅时间戳延迟；仅当该译词仍以当前原词为最近匹配时展示，避免把上一句译文复用到下一句。
- 网易云音乐和 QQ 音乐维持原有“曲名 + 歌手”精确搜索；未命中时才追加一次艺人搜索（最多 30 条）。仅接受歌手相符、时长相差不超过 8 秒、候选唯一且无 Live/Remix/Karaoke/Instrumental 版本冲突的本地化曲名候选。
- 因此 Apple Music 的“爱情傻瓜 / 蔡依林”可安全回退匹配歌词源中的 `Lovefool`；同歌手同时长的多个候选会拒绝，不会抓取不确定歌词。
- 新增独立 Core 契约回归：小幅译词延迟、稀疏译词不复用、网易云/QQ 艺人回退及歧义拒绝。

## 未修改 / 非目标

- 未修改任何 XAML、设置页、岛屿布局或 `LyricHover.App` 显示代码。
- 未调用第三方机器翻译；仍只消费歌词源返回的翻译轨。
- 未改变用户数据迁移、歌词缓存路径、商店身份 `70643607.LyricIsland`、应用标识或单实例名称。

## 验证

- 命令：`dotnet build LyricHover.Core.TranslationContractTests\LyricHover.Core.TranslationContractTests.csproj -c Release`；`dotnet run --no-build --project .\LyricHover.Core.TranslationContractTests\LyricHover.Core.TranslationContractTests.csproj -c Release`
- 结果：通过，0 警告、0 错误；`Core translation contract tests passed.`
- 命令：`$env:TargetPlatformSdkPath = 'C:\Program Files (x86)\Windows Kits\10\'; $env:TargetPlatformDisplayName = 'Windows'; dotnet build LyricHover.sln -c Release`
- 结果：通过，0 警告、0 错误。
- 命令：`$env:TargetPlatformSdkPath = 'C:\Program Files (x86)\Windows Kits\10\'; $env:TargetPlatformDisplayName = 'Windows'; dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：全量通过。回归基线中登记的 `translation mode explains why single line is unavailable` 与 `mouse avoidance settings fit without scrolling` 在当前工作树也通过；没有新增失败。

## 风险与后续

- 已知限制：艺人或时长缺失、候选并列，或版本标记冲突时会保守地不回退；用户可改用刷新歌词或其他来源，而不会误配歌词。
- 交接目标：桌面 UI 线程可用 Apple Music 显示“爱情傻瓜”的曲目做一次人工验收，并在含 1 秒左右翻译时间戳偏移的歌曲上确认中文译词随行出现。
- 回滚点：删除本任务对 `TimedLyrics`、`LyricsDisplaySelector`、`LyricsCandidateMatcher`、`NetEaseLyricsClient`、`QqMusicLyricsClient` 和 Core 契约测试的改动即可恢复至基线逻辑。
