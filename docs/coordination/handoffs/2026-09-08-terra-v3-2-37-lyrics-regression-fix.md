# Terra 初检：v3.2.37 歌词回归修复

- 日期：2026-09-08
- 基线：`ebc0932b573d4421c4a4e5dac9a3b26ab5bc3f66`
- 工作树：`.worktrees/fix-v3-2-37-regressions`
- 状态：待 Astra 独立复核；未提交、未推送、未改版本、未触碰真实用户设置、注册表或 Widgets。

## 修复范围

1. `LyricsTranslationMerger` 对同源完整原文序列逐行匹配，不再以短文本或少于三行丢弃可靠翻译。跨版本复用仍要求原有的长文本和最少三行证据，重复原文按行序和时间分别映射。
2. `WordTimedLyricsParser` 接受 `lineStart=0` 时数值完全等价的 QRC/YRC 绝对或相对偏移；保留真正不同偏移模式的歧义拒绝。相邻词仅允许不超过 50 ms 的格式化舍入重叠，仍拒绝倒序、越界和更大重叠。
3. `WordTimedPreferredLyricsClient` 在已取得可验证逐字包后，翻译回退获取异常时保留该包。
4. 遗留 Widgets 恢复失败时，歌词坞只要当前安全空位可用就显示，不获取、覆盖或重新写入新 lease；恢复文件继续保留。自动隐藏、全屏和无安全空间保持隐藏。设置文字区分“Widgets 恢复未完成”和“歌词坞环境不可用”。
5. pending 恢复期间，环境变化和用户确认后的 Settings 路径同样不能开始新的 Widgets acquire；仅允许对既有恢复记录的 restore retry。
6. 身份验证仍拒绝不同可读标题，只对末尾括号或方括号中的 `Explicit` / `Clean` 内容分级限定词允许等价；`Live`、`Remix`、`Karaoke`、`Instrumental` 仍拒绝。
7. pending 恢复期间的 `environment.Changed` 始终重新探测安全空位：安全时重显，非安全时隐藏；该路径不获取新 lease、不进入 Settings、也不覆盖恢复文件。
8. 短句同源映射改为有序的一对一翻译消费。每个原文行必须有独立、未消费且时间可靠的翻译，才能使用短句低门槛；缺译会回退到保守跨版本规则。

## 新增反例

- 两行短原文同源翻译保留，且逐字行继续存在。
- 翻译回退抛异常时，已验证的逐字包不丢失。
- 零起点 QRC/YRC 与 25 ms 相邻重叠可用；200 ms 重叠仍逐行降级。
- QQ 受控获取链返回完整逐字包，并通过既有 `LyricsPackageValidator`；未放宽身份验证。
- `TaskbarDa=Absent`、恢复文件为 `Enabled`、写入 ACL 拒绝时：歌词坞在安全空位显示，恢复文件内容不变，无 Settings 自动化或新 lease 获取。
- pending 时重新应用新屏幕/对齐会重新探测并显示新安全空位；无安全空位仍隐藏。
- 同一 pending 场景触发 `environment.Changed` 和直接 Settings UI 入口均不产生新写入、Settings 自动化或 lease 覆盖；自动隐藏后仍隐藏，安全空位恢复后重新显示。
- `You` 与同歌手的 `You (Explicit)` 接受且保留逐字与翻译；`You (Live/Remix/Karaoke/Instrumental)` 及既有 `晴天` / `夜曲` 冲突继续拒绝。
- pending 时先因 `InsufficientSafeSpace` 隐藏，随后只触发 `environment.Changed` 即可在安全空位恢复时重显，且没有新写入或 Settings 自动化。
- 两条 400 ms 相邻短句在只有一条翻译时不再复用该翻译；两条独立翻译按顺序分别映射。

## 验证

在 Windows SDK 环境变量已设置的隔离工作树中：

```powershell
dotnet build LyricHover.sln -c Release
dotnet run --project LyricHover.Tests -c Release --no-build
dotnet run --project LyricHover.Core.TranslationContractTests -c Release --no-build
```

结果：Release 构建 0 error（245 个现有链接型警告）；完整桌面回归 247 PASS、0 FAIL。翻译契约在前一轮 Core 状态通过；最后一次针对本轮一对一映射的重跑被自动审批服务连接中断拒绝，未将前一轮结果表述为本轮最终证据。

自动化不替代实际 Windows Widgets、DPI 与播放器切歌的现场验收。建议 Astra 按上述现场同构状态独立复核，尤其检查 pending surface 不触发 lease acquire 或 Settings 自动化。
