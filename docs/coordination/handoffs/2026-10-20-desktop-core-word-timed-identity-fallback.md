# 任务交接：逐字歌词身份冲突不再阻断有效来源回退

- 日期：2026-10-20
- 任务线程：desktop-lyrics-player-core
- 基线提交：`b287506c34c60b714914ace420aeed1b5a50336d`
- 结果提交：未提交（共享工作树交接）
- 允许修改范围：逐字优先组合、QQ/网易歌词候选选择、App 组合根、对应桌面回归测试与本交接文档

## 已完成

- 逐字歌词候选只有同时具备逐字时间并通过当前曲目的歌词包身份、时间范围与覆盖率校验后，才可进入逐字优先槽。
- 曲名、歌手或版本身份冲突的逐字包会被跳过，后续逐字来源和普通歌词回退仍继续执行，不再把最终可用结果覆盖成空歌词。
- 新增跨来源回归：QQ 风格的 `Style (Taylor's Version)` 逐字包不能覆盖 `Style` 原版的有效普通歌词。
- QQ 与网易云的普通歌词客户端保持原有单候选行为；只有请求逐字能力时，才按匹配分数最多探测 3 个去重候选。
- 第一候选只有普通 LRC、请求失败、逐字格式无效或身份校验失败时，会继续探测后续安全候选；找到有效 QRC/YRC 后仍由逐字优先组合执行完整覆盖率与回退校验。

## 未修改 / 非目标

- 未改变歌词来源优先级、QQ/网易云/LRCLIB 客户端搜索规则、缓存路径、设置格式或 UI。
- 未放宽不同 Live、Remix、重录版等身份冲突；没有可靠证据时继续拒绝可能串歌的歌词。
- 共享工作树中既有外观、发布及网站 WIP 均未纳入本任务。

## 验证

- 命令：`dotnet build LyricHover.sln -c Release`
- 结果：通过，0 error；保留当前测试工程既有类型冲突警告。
- 命令：设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 后运行 `dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：全部执行项 PASS，包括新增的 `rejects wrong-track word timing before using a valid fallback`。
- 命令：`dotnet run --project LyricHover.Tests -c Release --no-build -- --release-version-fixture`
- 结果：PASS；需在沙箱外读取当前用户现有 NuGet 缓存。
- 真实来源复核：用刚构建的 Core 对 Apple Music `Style / Taylor Swift / 198 秒 / 1989` 运行默认逐字优先与多来源回退链。
- 结果：返回 3538 字符的有效包，`LyricsPackageValidator` 接受；结果不再含 `Style (Taylor's Version)` 冲突标题。
- 多候选修复后的在线复核：用逐字专用 QQ 客户端再次查询同一 `Style`，返回 3940 字符、22 行的 QRC，`HasWordTiming=True`、`LyricsPackageValidator=True`，包内标题为 `Style`、歌手为 `Taylor Swift`；网易云本次没有返回可用逐字包。
- 新增回归：QQ 与网易云均覆盖“评分最高候选只有普通 LRC、第二个高可信候选带有效逐字数据”的场景；完整桌面测试全部 PASS。
- 命令：`dotnet build LyricHover.sln --configuration Release --no-restore`
- 结果：通过，0 warning、0 error。
- 命令：`dotnet run --no-build --no-restore --configuration Release --project LyricHover.Tests -- --release-version-fixture`
- 结果：PASS。

## 本地候选包

- 首轮身份回退修复生成过 `3.3.9.0`；同源多候选逐字修复完成后再次递增末尾版本号，当前产品版本为 `3.3.10`，Windows/MSIX 四段版本为 `3.3.10.0`。
- `tools/publish-next-version.ps1 -NoLaunch` 已完成，`publish/current` 已切换到 3.3.10 本地候选；未自动启动应用。
- `store/msix/build-msix.ps1 -SkipRestore` 已完成：完整桌面回归通过，自包含 `win-x64` 发布、MakeAppx 打包与解包校验均通过。
- 本地产物：`store/package/msix/LyricHover_3.3.10.0_x64.msix`。
- 大小：`87,998,527` bytes；SHA-256：`11420C7E634316B028E25CDB0CC5B62B345CFCD1C987DACE715ADE1301C9AD6E`。
- 包内身份：`70643607.LyricIsland`；架构：`x64`；App/Core DLL SHA-256 分别为 `FDCAF475C7F09EEF5EAC5E43777C929DAB9ECB982FE5A8F67E0B1DE06771FC2E`、`733A6CE10A8BE13D7BC348D29DF3166F44AEB72DE0FAAE94847982DBD9ABBD7E`，均与 `publish/current` 完全一致。
- 签名状态：`NotSigned`；该文件是本地候选，未提交、未推送、未创建 GitHub Release、未上传或提交 Microsoft Store。
- 旧 `3.3.9.0` MSIX 不含同源多候选修复，已被当前 `3.3.10.0` 本地候选取代。
- 尚未执行真实 MSIX 安装、升级与可视化 UI 验收。

## 风险与后续

- 已知限制：若所有在线来源都只提供身份冲突版本，应用仍会保守地显示无歌词；本修复不会把重录版、现场版或 Remix 冒充原版。
- 已知限制：歌词源存在普通歌词不等于匿名接口一定返回 QRC/YRC；最多 3 个可信候选均无有效逐字数据时仍会保留逐行歌词，不伪造逐字时间。
- 交接目标：在真实 Apple Music 播放中复核 `Style`，并抽查一首 QQ 首候选版本不符但后续来源存在正确歌词的歌曲。
- 回滚点：还原 `WordTimedPreferredLyricsClient` 的候选身份校验，并删除对应回归测试与本交接文档。
