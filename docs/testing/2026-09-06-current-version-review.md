# 当前版本整体复盘与缺陷检查

- 日期：2026-09-06
- Owner：Quality & Integration
- 基线：`main` / `d78e57a85cccca8871b8ef400c9df85bd6ef3aa2`，开始时工作区干净。
- 版本：`3.2.36-Beta`。
- Task Contract：检查当前桌面端和官网的构建、回归、关键用户交互与近期改动；记录可复现缺陷、环境失败和未覆盖验收。
- 允许修改范围：本测试报告、独立交接记录与 `artifacts/qa-20260906-d78e57a/` 临时证据/探针。
- 业务修复交回对应模块 Owner；本轮不生成发布候选，不执行外部发布。
- 验证：同一 SHA 的 Release 构建、完整桌面回归、独立翻译契约、官网 typecheck/API/安全检查/ESA 构建，以及可执行的实机窗口检查。

## 结论

已复现 **3 个产品功能缺陷、1 个测试缺陷**。现有完整桌面套件 **221 PASS / 0 FAIL**，不能据此认定没有 bug。建议先处理缓存串歌，再处理歌词内容保留与请求失败回退，补齐实机验收后再评估发布。

本轮为检查与复现，未修改业务代码、正式测试断言、版本或 `publish/current`，未提交、推送或发布。下列缺陷属于当前版本既存问题，没有证据将其归因于当天的播放按钮补丁。

## 版本与证据范围

- Windows `10.0.26200`，x64；.NET SDK `3.1.426`，桌面运行时 `3.1.32`；Node `v22.22.0`，npm `10.9.4`。
- 功能基线固定为 `d78e57a85cccca8871b8ef400c9df85bd6ef3aa2`。执行期间其他任务将交接文档提交至 `main`；复核 `LyricHover.Core/`、`LyricHover.App/`、`LyricHover.Tests/`、版本文件与官网源码均无相对此功能基线的变化。
- 当前进程时点证据：PID `6660`，路径 `D:\AppleMusicDesktopLyrics\publish\current\LyricHover.App.exe`。
- 当前 App DLL SHA-256：`42DF3874C52BF5871B6D72E0CB3BD34CFEF570C083BCF97DD83800669A465AC4`，与已有 `win-x64` Release 输出相同。
- 当前 Core DLL SHA-256：`5EA13EB02A27723AF4E75FF4AFED8E8533B4BF21F814DF89939DCDF261BC8866`，与本次构建的 Core 输出相同。
- 普通 Release App DLL 与 `win-x64` App DLL 哈希不同，不能将不同构建目标的二进制混为一份证据。实机尝试针对上述 `publish/current`；自动化针对本次普通 Release 构建。
- 已读取补充交接 `docs/coordination/handoffs/2026-09-06-desktop-island-playback-refresh-interaction.md`。其中 81 个命中点及反向验证为来源任务证据；本轮完整套件重新执行了相应命中测试，但未重新执行其反向改码实验。
- 本地原始日志、合成输入与复现程序保留在 `artifacts/qa-20260906-d78e57a/`，不应当作业务源码或发布包提交。

## 缺陷 1：中文歌曲缓存身份发生碰撞（P1）

**Evidence**：`LyricHover.Core/LyricsCache.cs:46,203-220` 用只保留 ASCII 字母和数字的 `Slugify` 生成唯一缓存路径。全中文歌名、歌手被折叠消去；`TryRead` 直接返回文件内容，并额外探测相差 1～2 秒的缓存。`MainWindow.xaml.cs:651-669` 未校验缓存的曲目元数据与当前歌曲一致。

**复现**：在独立临时缓存中构造两条合成身份（用于验证键规则，不代表两首真实录音时长相同）：

```text
A = 晴天 / 周杰伦 / 180 秒
B = 红豆 / 王菲 / 180 秒
向 A 写入 [ti:晴天][ar:周杰伦] 和一行同步歌词，再读取 B。
CACHE_COLLISION samePath=True file=180.lrc wrongTrackHit=True returnedTitle=晴天
```

改为另一条 181 秒的中文曲目也会读取该缓存：`CACHE_NEAR_DURATION wrongTrackHit=True`。组合客户端同样会接受与请求不符的可读 `[ti:]` / `[ar:]` 歌词，探针记录 `COMPOSITE_MISMATCH returnedTitle=红豆`。

再把 A 的缓存改为中文逐字包，读取 B 得到 `wrongText=晴天的歌词 needsWordRefresh=False ignoresTranslation=True`。这确认默认逐字/翻译补取条件也可能直接放行错误缓存，无须依赖网络故障才能触发。

**Impact**：切歌后显示另一首歌曲歌词；写入时还会覆盖另一首歌的缓存。关闭逐字追踪、无需补翻译、网络无法提供新歌词或错误缓存本身已含逐字数据时，额外刷新条件不能可靠消除串歌。App 的异步 generation 防护只检查请求身份，无法识别错误内容所属歌曲。

**Suggested Owner**：Desktop Core（缓存键、身份校验与旧缓存迁移），Desktop UI 消费契约；Quality 增加不同中文身份/相邻时长/错误元数据的行为回归。建议采用完整规范化身份的稳定哈希，并对可读元数据冲突 fail closed；迁移旧键时也必须验证身份。

## 缺陷 2：QQ 主接口成功后，备用请求失败会丢弃有效歌词（P2）

**Evidence**：`LyricHover.Core/QQMusicLyricsClient.cs:51-98` 已取得并验证主接口歌词后，只要候选有 `Mid`，仍会请求 legacy 接口。该请求的网络异常、取消或 JSON 异常落入整个方法的外层 catch，直接返回空字符串。

**复现**：使用客户端已有的注入请求委托，搜索返回正确 `Song / Artist / 180 秒`，第二次请求返回可解析同步歌词，第三次 legacy 请求抛出 `HttpRequestException`。输出：

```text
QQ_VALID_PRIMARY_LEGACY_FAILURE calls=3 retained=False
```

**Impact**：一个备用端点故障即可抹掉已成功取得的歌词；其他来源也无结果时，用户看到“未找到同步歌词”，并可能引发重复请求。

**Suggested Owner**：Desktop Core。将备用请求的异常处理收窄到该请求；已验证的主包应作为成功回退值保留。补测主包成功且备用接口超时、网络异常、无效 JSON 的组合。

## 缺陷 3：逐字解析未保留完整原文（P2）

**Evidence**：`LyricHover.Core/WordTimedLyricsParser.cs:13,27-33` 只拼接正则匹配到的词。没有词标记的行会被跳过；QRC 最后一个标记后的文本、YRC 正文里的括号文字不会进入输出。包中只要存在一条成功逐字行，`LyricsPackageParser.ParseOriginalLyrics` 就返回这份不完整结果。

**复现**：调用真实 `LyricsPackageParser.Parse`：

| 合成输入 | 实际输出 | 丢失内容 |
| --- | --- | --- |
| `[1000,1000]你(1000,500)好` | `你` | 未带词时间的尾字 `好` |
| `[1000,1000](1000,1000,0)你好(合唱)` | `你好` | 正文 `(合唱)` |
| 第一行 `[1000,500]你(1000,500)`，第二行 `[2000,500]完整第二句` | 只有第一行 | 有行时间但没有词时间的第二句 |

**Impact**：歌词不完整或整句消失；存在局部异常时未按“Words 可选、原文保留”的约定退回逐行展示。即使提供方同时返回完整普通 LRC，当前有效性判断也可能认为这份残缺逐字包有效。

**Suggested Owner**：Desktop Core。分离行正文保留与词时间解析；逐字验证失败时保留行文字或选择完整 LRC，不直接丢行/丢字。Quality 覆盖括号、标点、未定时尾部和逐字/逐行混合输入。

## 缺陷 4：独立翻译契约断言与当前请求链不一致（P2，测试缺陷）

**Evidence**：`LyricHover.Core.TranslationContractTests/Program.cs:229` 要求 QQ 本地化标题回退恰好发生 3 次请求。当前链为普通搜索、歌手搜索、主歌词、legacy 歌词，共 4 次；该用例前一个 `result.Contains("Lovefool")` 断言已经通过。

- 原工程未经修改执行失败：`QQ Music localized title fallback did not use the artist query.`
- 原程序按顺序执行，7 项完成后第 8 项抛错，第 9 项未执行；不能将整个独立契约套件标为通过。
- 在临时复现副本中**仅**把这条 QQ 计数断言从 3 改为 4，保留歌手查询与歌词内容断言，并执行全部 9 项，结果通过。该副本不是正式修复，也不改变原套件失败状态。
- 该测试工程未加入 `LyricHover.sln`，所以常规 221 项桌面套件通过时不会暴露此失败。

**Impact**：额外契约门禁持续误报，且常规验证遗漏该工程。不能据此认定 QQ 的本地化歌名回退失效。

**Suggested Owner**：Quality + Desktop Core。更新为验证请求职责/顺序与输出的行为断言，并纳入统一验证入口。

## 其他探针观察（不与上述功能缺陷混计）

- 超大毫秒时间值 `9223372036854775807` 会使逐字解析抛出 `OverflowException`；建议 Core 增加时间边界和异常回退。
- 逐字包的 `[ti:]` / `[ar:]` 元数据目前丢失，应结合缺陷 1 的身份校验修复处理。
- 给 `WordTimedPreferredLyricsClient` 注入会抛异常的 fallback 时，已取得逐字包仍会被异常覆盖；生产 fallback 为会捕获常见提供方异常的 `CompositeLyricsClient`，因此此项只记录为契约健壮性风险，不单独宣称是当前真实网络路径已复现的故障。
- 缓存键最后修改于 `3782e6e`；逐字解析和 QQ 相关实现最后修改于 `1de8193`。以上缺陷并非本轮点击范围提交修改的代码。

## 验证矩阵

| 检查 | 本轮结果 | 原始证据 |
| --- | --- | --- |
| `dotnet build LyricHover.sln -c Release`，设置 Windows SDK 环境变量 | 退出 0；本次为增量构建，输出 0 warning / 0 error，不代表全量重编译没有既有警告 | `desktop-build-unrestricted.log` |
| `dotnet run --project LyricHover.Tests -c Release --no-build`，未设置跳过 fixture | **221 PASS / 0 FAIL**，包含发布事务与序列化 fixture | `desktop-tests-unrestricted.log` |
| 独立翻译契约 | 退出 1；如上所述为过时计数断言 | `translation-contract-unrestricted.log` |
| 独立缺陷探针 | 确认串歌、原文丢失、QQ 成功结果丢失；临时翻译诊断副本 9 项通过 | `probes-final.log`、`probes/Program.cs`、`probes/TranslationDiagnostic.cs` |
| `npm ci --no-audit --no-fund` | 安装 351 个锁定依赖；lockfile 未修改 | `web-npm-ci-unrestricted.log` |
| `node node_modules/typescript/bin/tsc --noEmit` | 退出 0 | `web-typecheck-direct.log` |
| `node scripts/test-esa-api.mjs` | 退出 0，ESA API tests passed | `web-api-direct.log` |
| `node scripts/check-support-security.mjs` | 退出 0，Support security checks passed | `web-support-security.log` |
| ESA 本地构建（直接 Node 启动 Next 的临时脚本） | 退出 0，17/17 静态生成步骤完成；API 源码目录已恢复 | `web-build-direct-node.log`、`build-esa-direct-node.mjs` |
| `git diff --check` | 退出 0 | 本轮终端记录 |

环境失败单独保留：沙箱内 NuGet 配置、SDK 与包缓存访问受限，获准外部运行后构建和完整套件完成。npm 包装命令与 ESA 原脚本的 `.cmd` 子进程无法找到 Node；即使尝试规范化 PATH/沙箱外运行仍失败。最终临时构建脚本只固定工作根、使用 `process.execPath` 直接启动现有 Next CLI 并关闭 shell，Next 配置、源文件、ESA 替换与 API 暂存/恢复逻辑均保持一致。不能将这个结果表述成未经调整的 `npm run build:esa` 原命令通过。

探针重新执行：

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet run --project artifacts/qa-20260906-d78e57a/probes -c Release --no-restore
```

## 实机验收与未覆盖范围

- Computer Use 能读取和操作 QQ 音乐；本轮短暂播放后已确认恢复暂停。该行为不等同于通过歌词岛播放按钮测试。
- 运行中的歌词岛未出现在工具的可定位窗口列表中；重新唤起后仍返回 `launched app did not expose a targetable window`。没有猜测窗口句柄或点击坐标，也未用其他方式绕过工具限制。
- 因此 **点击穿透、Ctrl + 歌词边缘刷新、Ctrl + 上一曲/暂停/下一曲的单次派发、暂停 10 秒保持、100%/150% DPI、设置 Apply/Cancel 实际窗口、歌词坞实机表现仍未验收**。已有 81 点 WPF 命中回归只能提供控件层证据。
- Browser 初始化失败，运行时尝试导入缺失的 `browser/26.901.31953/scripts/browser-service.mjs`。本轮没有官网实际渲染截图，也没有生产缓存或线上路由验收；本地构建/API 测试不替代它们。
- 未执行多播放器/多显示器长期压力、网络提供方完整实时矩阵、MSIX 安装或 Microsoft Store 发布验收。
- 实机未通过部分应保持“待验收”，不推断为产品必然故障，也不标记为通过。

## 工作区与后续

保留其他任务对 `docs/desktop-core-domain-knowledge.md` 的修改，以及新增托盘菜单交接；本轮不将其收录、重置或提交。官网构建完成后 `website/app/api` 存在，`website/esa-source-staging/api` 不存在，源目录恢复正常。`publish/current` 的上述 App/Core 指纹复核未变。

交接目标：Desktop Core 优先处理缺陷 1～3；Quality 处理缺陷 4 并补行为回归；Desktop UI + Quality 完成真实交互矩阵。修复应在独立功能任务中完成，随后基于修复 SHA 重跑对应行为探针和完整回归。
