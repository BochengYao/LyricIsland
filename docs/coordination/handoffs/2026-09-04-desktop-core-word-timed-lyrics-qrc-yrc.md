# 任务交接：Desktop Core / QQ QRC、网易 YRC 逐字歌词与兼容降级

- 日期：2026-09-04
- 来源线程：逐字歌词追踪接入、翻译/强刷恢复与显示修复
- 交接目标：Desktop Lyrics & Player Core
- Handoff Status：**Feature Handoff / 待 Core 审核**
- 基线提交：`815444f9363d3567772296924731d472c205f649`
- 结果提交：未提交；当前为 `main` 上的混合未提交工作树，不能由结果 SHA 审计。
- 当前本地候选：`publish/current`，`3.2.32-Beta`，framework-dependent `win-x64`。
- 候选哈希：`LyricHover.Core.dll` SHA-256 `76515357B41B4B20425A1C9D52AF3098B7C113C066242AC1FFE44D82A9C39142`。
- 渠道状态：仅已构建、已测试、已打包到本地；未上传、未提交或发布到 GitHub、Microsoft Store、官网等外部渠道。

## Task Contract

- Goal：在保留普通 LRC 和既有缓存行为的前提下，为主歌词增加可选逐字时间片段，接入 QQ QRC 与网易 YRC，并在不可用时可靠降级。
- Owner：Desktop Lyrics & Player Core。
- Allowed Write Scope：`LyricHover.Core/` 的歌词模型、歌词包解析、QQ/网易客户端、逐字优先组合客户端及直接回归测试。
- Forbidden Scope：不改变歌词岛/歌词坞视觉布局，不在 Core 中复制 UI 设置状态，不接入酷狗 KRC 解密，不修改商店身份和用户数据目录迁移。
- Definition of Done：QRC/YRC 解析和异常回退有测试；旧 LRC 缓存仍可显示；翻译仍按现有逐行轨道合并；源失败不得把可用普通歌词替换为空结果。

## 已完成

### 1. 向后兼容的逐字模型

- `LyricLine` 保留原有 `Timestamp`、`Text` 和行级选择语义，新增可选 `Words`。
- `LyricWord` 保存相对行开始时间、时长和文本；`LyricLine.HasWordTiming` 用于能力判断。
- `LyricLine.GetWordProgress(position)` 按真实片段时间计算行内进度，调用方无需伪造均匀逐字动画。
- 未携带逐字片段的既有 `LyricLine` 构造和 LRC 解析仍按原行为工作。

相关文件：

- `LyricHover.Core/LyricLine.cs`
- `LyricHover.Core/LyricWord.cs`
- `LyricHover.Core/TimedLyrics.cs`

### 2. QRC/YRC 解析与普通 LRC 回退

- `WordTimedLyricsParser` 识别 QQ QRC 的 `[行开始,行时长]文本(词开始,词时长)` 和网易 YRC 的 `[行开始,行时长](词开始,词时长,0)文本`。
- 同时接受提供绝对词时间和旧缓存可能使用的相对词时间。
- `LyricsPackageParser` 先尝试逐字格式；无有效逐字行时继续使用现有 `LrcParser`。
- 翻译分隔符、翻译语言元数据和翻译 LRC 轨保持现有格式；只有原文主轨携带逐字片段。

相关文件：

- `LyricHover.Core/WordTimedLyricsParser.cs`
- `LyricHover.Core/LyricsPackageParser.cs`
- `LyricHover.Core/LyricsTranslationMerger.cs`

### 3. QQ 与网易获取

- QQ 请求启用 QRC；兼容 `qrc` 仅为可用性标志、实际密文位于 `lyric` 的响应。
- `QqQrcDecrypter` 处理当前 QQ QRC 的十六进制/压缩加密载荷；解密、解压或格式验证失败时继续使用 QQ 普通歌词。
- 网易歌词请求启用 `yrc=true` 并保留 64 位歌曲 ID；只有 YRC 可验证为逐字歌词时才替换普通原文。
- 翻译仍读取提供方原生翻译字段，并通过现有歌词包规则合并。

相关文件：

- `LyricHover.Core/QQMusicLyricsClient.cs`
- `LyricHover.Core/QqQrcDecrypter.cs`
- `LyricHover.Core/NetEaseLyricsClient.cs`

### 4. 逐字候选优先与缓存兼容

- `WordTimedPreferredLyricsClient` 在 UI 请求逐字能力时先尝试 QQ/网易逐字候选。
- 找到可验证逐字候选即返回；否则恢复既有用户源优先级与翻译合并结果。
- 缓存仍保存原始歌词包字符串，没有引入第二套缓存格式；旧 LRC 缓存可自然解析为行级歌词。
- 开启逐字能力后，App 会识别旧缓存不含逐字数据并尝试刷新；刷新为空时不会覆盖已有可用包。

相关文件：

- `LyricHover.Core/WordTimedPreferredLyricsClient.cs`
- `LyricHover.App/MainWindow.xaml.cs`（仅为 Core 契约消费证据）

## 回归证据

`LyricHover.Tests/Program.cs` 已覆盖：

- QQ QRC 行时间、词时间和行内进度。
- 网易 YRC 解析。
- QQ `qrc=1`、密文不可解析和普通歌词回退。
- 网易 64 位 ID 与 `yrc=true` 请求。
- 逐字原文与普通翻译轨合并。
- 逐字候选优先、无逐字候选时回到常规源。
- 旧 LRC、无翻译、无逐字数据的兼容行为。

候选验证：

| 命令 / 检查 | 结果 |
| --- | --- |
| `dotnet restore LyricHover.App\LyricHover.App.csproj -r win-x64` | 成功。 |
| `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1 dotnet run --no-restore --configuration Release --project LyricHover.Tests` | 所有执行的产品回归均 PASS；发布事务夹具按候选生成流程跳过。测试工程仍输出既有 `CS0436` 重复类型警告。 |
| `dotnet build --no-restore --configuration Release --runtime win-x64 LyricHover.App\LyricHover.App.csproj` | 成功，0 warning、0 error。 |

## 未修改 / 非目标

- 酷狗仍只使用现有普通歌词链路；未接入 KRC 解密。
- 翻译不逐字追踪。
- 未改变缓存路径、容量清理、曲目身份、媒体时间线或用户数据迁移格式。
- 未证明 QQ/网易接口具有长期稳定性或服务承诺。

## 风险与后续

| Evidence | Impact | Suggested Owner |
| --- | --- | --- |
| QQ QRC 解密及响应字段依赖当前服务实现 | QQ 调整加密或字段后可能只剩普通歌词，但不应导致整首歌词丢失 | Core |
| 网易只有返回有效 `yrc.lyric` 才采用逐字结果 | 部分歌曲和地区只能显示逐行歌词，这是预期降级 | Core + Quality |
| 自动化覆盖了解析和组合，但 `3.2.32` 打包后未完成 QQ/网易各一首的在线实机复核 | 当前证据不能替代真实网络、真实账户和真实曲库验收 | Core + Quality |
| 当前实现位于混合未提交 `main` 工作树 | 不能作为可审计集成提交，后续整理时必须保留其他 WIP | Integration |

建议 Core 接手后：先审查 `LyricLine.Words` 的稳定契约和 QRC 解密失败边界，再使用 QQ/网易各一首逐字歌曲及一首 LRC-only 歌曲执行在线实测；如契约接受，交给 Architecture 更新共享桌面契约。

## 回滚点

- 可回滚新增的 `LyricWord`、逐字解析和逐字优先客户端，并让 `LyricsPackageParser` 始终回到 LRC；不得删除既有缓存文件。
- 回滚 Core 时必须同时通知 UI 将逐字开关降级为不可用或移除，避免设置仍承诺不存在的能力。
