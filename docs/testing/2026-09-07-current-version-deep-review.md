# 3.2.36-Beta 第二轮边界与异常检查

- 日期：2026-09-07。
- Owner：Quality & Integration。
- 基线：`7343def`（此前 `d78e57a` 后仅文档变更，功能源码未变）。
- Task Contract：延续第一轮复盘，新增异常输入、异步请求、缓存恢复、时间同步、设置状态、歌词坞生命周期及官网 API 边界验证。
- 允许写入：本测试报告、独立交接、`artifacts/qa-20260907-deep/` 复现辅助材料。保留已有 WIP 与第一轮证据；不修改业务实现、版本、实际用户设置或发布候选。
- 验证方式：真实程序集上的行为探针、受控并发/错误请求、临时缓存，以及本地 mock 官网接口。仅在测试输入均合成的环境中触发写操作。

## 结论

本轮在明确的合成输入、异常和并发条件下，复现 **14 项新增问题场景（桌面 9 项、官网 5 项）**，其中 2 项建议 P1、12 项建议 P2。这里统计的是可分别验收的行为问题，不代表 14 个互不相关的根因，也不代表线上已发生 14 次事故。第一轮的 3 项功能缺陷及 1 项测试断言问题仍未修复；本轮不是修复验收。

优先处理 Windows 小组件恢复记录丢失和兑换码删除/分配竞态。歌词缓存、逐字显示、翻译、官网数据一致性问题随后按各模块所有权修复。不能据现有测试宣称“所有 bug 已找尽”或当前版本可发布。

## 基线与证据边界

- HEAD：`7343def786e853f236ea1707f76f3e1ee919368e`；与第一轮 `d78e57a85cccca8871b8ef400c9df85bd6ef3aa2` 相比，提交差异仅为两份交接文档。
- 本轮调用普通 Release 输出的真实程序集：App SHA-256 `2BF23B807B6FC0849F6CEB8B162317C84A86D257ACEA992657CD8A3199DEA844`；Core SHA-256 `5EA13EB02A27723AF4E75FF4AFED8E8533B4BF21F814DF89939DCDF261BC8866`。普通 Release App 与 `publish/current` 的 win-x64 App 不是同一构建配置，不能混用哈希。
- 桌面探针跳过 `MainWindow` 构造，注入临时缓存、设置和请求接口，调用真实私有 `LoadLyricsAsync`；另直接执行真实 Core、设置存储、WPF 控件及歌词坞控制器。未启动额外产品实例，未修改实际用户设置、注册表或小组件状态。
- 官网运行实际 `website/esa/api.js` 的本地副本，替换配置为合成值；所有 fetch 被拦截，非 `https://qa.invalid` 地址会抛错。认证删除场景仅使用合成会话。数据库并发状态由确定性 mock 模拟，没有访问生产数据库或发布网站。
- `website/lib` 对应实现通过源码对照确认同类路径存在；本轮 API 执行证据直接来自 ESA 实现。TSV 探针执行实际 TypeScript 解析器的转译结果。
- 主要证据：[桌面执行日志](../../artifacts/qa-20260907-deep/app-probes-chain-verified.log)、[官网执行日志](../../artifacts/qa-20260907-deep/web-probes.log)、[桌面探针](../../artifacts/qa-20260907-deep/app-probes/Program.cs)、[歌词坞探针](../../artifacts/qa-20260907-deep/app-probes/LeaseProbes.cs)、[控件探针](../../artifacts/qa-20260907-deep/app-probes/VisualProbes.cs)、[官网探针](../../artifacts/qa-20260907-deep/web-probes.mjs)。这些 artifacts 是本地辅助证据，未纳入源码提交。

## 新增问题清单

| ID / 优先级 | Evidence | Impact | Suggested Owner |
| --- | --- | --- | --- |
| D01 / P2 | 真实 LrcLib → Composite → MainWindow 调用链收到非空无效歌词，`providerCalls=1 displayLines=0 cache=malformed nonempty payload` | 刷新把有效缓存覆盖成不可展示的内容 | Desktop Core + Desktop UI 加载边界 |
| D02 / P2 | 损坏缓存下强制刷新仍 `networkCalls=0`，坏缓存不变 | 用户点击刷新也无法自愈，只能另行清理缓存 | Desktop UI 加载流程 + Desktop Core 解析容错 |
| D03 / P2 | 两个同曲刷新新请求先完成：显示 `new refresh`，磁盘却留下 `old refresh` | 过期请求污染后续缓存，重载又回到旧歌词 | Desktop UI 请求代次与 Core 缓存边界 |
| D04 / P2 | 逐字原文在 10/20 秒，另一源原文/翻译在 12/22 秒，直接拼包后 `hasTranslation=True`，10.5 秒无翻译 | 合并不同来源的时间轴导致翻译丢显/错位；缓存却被视为已有翻译 | Desktop Core |
| D05 / P2 | 声称兼容的相对词时间输入解析出 `offsets=0,1000`，预期 `0,2000` | 旧相对时间载荷后面的词提前高亮 | Desktop Core；需明确格式契约 |
| D06 / P2 | `iiiiWWWW` 第一词结束时实际词宽 46.51，亮色裁剪宽 112.93 | 非等宽字形提前点亮下一词，英文及混排尤为明显 | Desktop UI 字形测量；Core 进度契约配合 |
| D07 / P2 | 合法设置文件被独占占用，`OverlaySettingsStore.Load()` 抛出未被恢复分支处理的 `IOException` | 设置读取失败可能扩大为启动失败，未退回默认设置 | Desktop UI 设置存储 |
| D08 / P1 | 模拟设置 UI 已关闭小组件，但后续验证失败：`recoveryKept=False`、恢复返回成功、最终仍 `Disabled` | 丢失原始 Windows 状态的恢复证据；退出/重启恢复也无据可依 | Desktop UI 歌词坞；Release 协作验证系统恢复 |
| D09 / P2 | 首次隐藏验证未完成时再次 Configure(true)，第二次成功后旧任务恢复小组件：`enabled=True finalState=Enabled` | 歌词坞仍开启却撤销了新一代请求的隐藏状态 | Desktop UI 歌词坞生命周期 |
| W01 / P2 | 100 条较新 pending + 1 条较旧 accepted/public，公开 API 返回 `visible=0` | 待审核量较大时已公开建议消失 | Website Admin & Data |
| W02 / P2 | 两个独立访客并发点赞均 200，`insertedLikes=2 storedLikeCount=1` | 点赞明细与汇总数量不一致，后续重试也不补计 | Website Admin & Data |
| W03 / P1 | DELETE 先读 available，模拟中途分配后 `stateAtDelete=assigned deleted=true conditionalGuard=false` | 删除已分配兑换码及其分配状态 | Website Admin & Data |
| W04 / P2 | TSV 日期 `2026-02-31`、`2026/13/40` 均 `errors=[]` | 预览放行非法日期，把错误延迟到入库或后续使用 | Website Admin & Data |
| W05 / P2 | `/api/features` 的 title/body/summary label 仍返回合成 `LRCLIB`，同条 items 已被替换 | 公开歌词源隐藏策略没有覆盖全部可渲染字段 | Website Frontend + Website Admin & Data |

### D01–D03：缓存刷新与异步状态

位置：`LyricHover.App/MainWindow.xaml.cs:646–679`，尤其 652 行先解析缓存、663 行先写缓存、667 行之后才检查请求代次。

1. D01：写入 `[00:01.00]cached good`；真实 `LrcLibClient` 的请求委托返回标题/歌手/时长匹配但 `syncedLyrics` 为 `malformed nonempty payload` 的合成 JSON。经过真实 `CompositeLyricsClient` 后刷新主窗口。结果缓存被覆盖、显示行数为 0。`LrcLibClient.cs:79–98` 与 `CompositeLyricsClient.cs:58–72` 接受非空结果，主窗口也未先验证可展示性。另一个直接注入异常客户端的边界探针表明：异常刷新会清空显示但保留磁盘缓存；普通 Composite 会吞单源异常，因此不把这个注入结果泛化为“所有断网都会清空歌词”。建议在接受响应、落盘和替换显示前验证候选，保留有效旧值。
2. D02：缓存为 `[9223372036854775807,1000]x(1000,1000)`；强制刷新客户端准备返回有效歌词。缓存解析先抛异常，根本未进入网络请求。建议坏缓存按未命中处理，隔离解析错误，让强制刷新可继续。本例为损坏/极值缓存，不宣称正常供应商会返回这个数字。
3. D03：同一曲目启动 generation 1/2；2 先完成写入新值，1 后完成写入旧值。显示代次保护有效，缓存代次保护缺失。建议在每个有副作用的异步提交边界重新验证代次/身份，并给同曲并发请求定义一致策略。

### D04–D06：逐字与翻译

- D04：`LyricHover.Core/WordTimedPreferredLyricsClient.cs:35–50` 直接取另一来源的翻译，未把它映射到所选逐字原文行。相同两句原文的时间分别是 10/20 秒和 12/22 秒；10.5 秒时 selector 无匹配翻译。建议按实际匹配原文行重定位源内翻译，无法可靠对应则保留可用回退组合；不要伪造逐字翻译。
- D05：`LyricHover.Core/WordTimedLyricsParser.cs:105–111` 按“词时间是否处在线时间范围内”逐词猜绝对/相对值。输入 `[1000,4000]a(0,2000)b(2000,2000)` 中第二词相对偏移 2000 被减去行起点 1000。本问题依据代码注释承诺的旧相对载荷兼容，不是对 QQ/网易云当前绝对格式的线上失败报告。修复需确定格式依据，不能继续靠范围猜测。
- D06：`LyricHover.Core/LyricLine.cs:35–58` 用字符数作进度权重，`LyricHover.App/Modules/WordTrackingTextBlock.cs:183–198` 再乘整行像素宽。使用 Segoe UI 48、两词各 1 秒，在第 1 秒冻结渲染；前 4 个 i 实际仅宽 46.51，但裁剪覆盖半行 112.93。建议把词边界映射为真实字形的视觉位置，不能以字符占比等同像素占比。

![第一词结束时亮色已进入 W；此图是离屏控件渲染，不是实机浮层验收](../../artifacts/qa-20260907-deep/word-boundary.png)

### D07–D09：设置和系统状态恢复

- D07：`LyricHover.App/OverlayPlacementSettings.cs:286–355`。以 `FileShare.None` 持有临时合法 `{}` 文件，Load 的读取失败进入 catch，catch 内备份 `File.Copy` 再次失败并向外抛出。恢复分支需独立处理备份不可用，区分暂时锁定和内容损坏；不能把备份成功当作恢复默认值的前提。
- D08：`LyricHover.App/LyricDock/WidgetVisibilityLease.cs:109–116`。Fake environment 原状态 Enabled，设置 UI 操作已把它改为 Disabled；视觉验证返回 false，失败分支直接删 recovery。之后 `TryRestore()` 因“无恢复文件”返回 true。建议实际状态可能已改变时先回滚并验证，或保留恢复记录；恢复不了时不可删除唯一证据。
- D09：`LyricHover.App/LyricDock/LyricDockController.cs:211–237`。用事件屏障暂停首次验证，第二次 Configure(true) 已成功，再释放旧验证。旧 continuation 发现 generation 过期却对共享 lease 执行 TryRestore，撤销了新任务的有效状态。建议串行化租约获取/恢复或明确租约代次所有权；过期任务不能恢复后继仍在使用的租约。

### W01–W05：官网数据与公开内容

- W01：`website/esa/api.js:695–715`、`website/lib/incentive-store.ts:324–334`。先对所有状态 `limit=100`，再本地筛选 accepted/public 并取 24。应在数据库先筛选可公开行；若公开标志仍封装在元数据内，需分页扫描或建立可查询字段，不能只补 accepted 过滤而遗留私密记录占满窗口的问题。
- W02：`website/esa/api.js:749–774`、`website/lib/incentive-store.ts:363–390`。两个请求同时读取 count=0，分别插入不同 voter 的明细，再各 PATCH count=1。应以事务更新明细与原子计数。仓库 `website/supabase/schema.sql:36–83` 已有 `toggle_incentive_like` 原子增量函数，但当前路径未调用；复用前仍需补齐当前公开性条件并核对部署状态。
- W03：`website/esa/api.js:1932–1940`、`website/lib/promo-code-store.ts:416–437`。状态校验与删除不在同一事务，DELETE 仅带 id 条件。模拟分配在读/删之间完成，已分配行仍被删除。应以数据库条件/事务保证删除时仍 available，并检查实际受影响行数；审计也应与实际结果一致。仓库 schema 的日志表没有该码的外键保护；本轮没有核查生产数据库是否另有仓库外约束。
- W04：`website/lib/promo-code-tsv-parser.ts:90–118`。JavaScript Date 的自动进位不等于日期组件合法性；当前只检查 NaN 后返回原字符串/拼接结果。应做年月日及时间组件回读相等校验，预览与导入共用此验证。正常 `2026-09-07` 控制样本仍成功。
- W05：`website/esa/api.js:189–207`、`website/data/feature-content.ts:115–133` 仅替换 items；`website/components/ManagedFeatureContent.tsx:210` 再使用同一不足的客户端过滤，后续 title/body/label 仍参与本地化及渲染。合成数据把 `LRCLIB` 放入标题、正文和摘要标签，公开 API 原样输出。应覆盖所有公开字段和语言，同时保留后台原文。此结果不证明生产当前存储了这些测试文本。

## 已通过的控制场景与暂不计入问题的观察

| 场景 | 实际结果 | 结论边界 |
| --- | --- | --- |
| 有效缓存 + 空刷新 | `display=cached good cachePreserved=True` | 空响应回退有效；不能推导异常/坏响应也安全 |
| 同曲并发显示 | 当前显示保持 `new refresh` | 显示代次保护有效，D03 针对磁盘缓存 |
| 暂停/恢复时间线 | 暂停为 11 秒，模拟 60 秒后仍 11，恢复推进到 12 | 单调时钟控制样本正确；非真实播放器切换验收 |
| 设置保存失败回滚 | runtime/committed 均恢复 0.25，同步调用两次 | 此 ApplyDraft 回滚路径有效 |
| WPF 长短行布局 2000 次 | `completed=True finalWidth=520` | 有限离屏控件压力测试通过；不证明整机常驻无泄漏 |
| 10 条后台路由未认证 GET | 全部 401，未触及 mock 存储 | 验证这些请求的鉴权入口；非完整鉴权/渗透测试 |
| 合法 TSV 日期 | `errors=[] date=2026-09-07` | 正常日期控制样本通过 |

另有三项观察，未计入上述 14 项：

1. `LyricDockController.Dispose()` 后 surface 事件仍能转发一次；尚未证明产品正常生命周期中会再次触发，作为 P3 生命周期加固候选。
2. 直接程序化赋值 `OffsetRatio=NaN` 后 Normalize 未消除 NaN；尚未证明默认 JSON 和 UI 输入路径可产生该值，不列普通用户可触发缺陷。
3. 设置窗口当前显式自动保存，Apply/Cancel 按钮已隐藏；不能仅凭历史 Draft/Cancel 约定就断言“点击取消失效”。新旧约定的统一交由架构/UI Owner，未据此扩大问题数量。

## 执行命令与结果

在仓库根目录：

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet run --project artifacts/qa-20260907-deep/app-probes -c Release --no-restore
node artifacts/qa-20260907-deep/web-probes.mjs
git diff --check
```

- 桌面最终补强日志为 `app-probes-chain-verified.log`：退出 0，`HARNESS_ERRORS=0`，仅有两个测试 fake 未使用事件的 CS0067 警告。**退出 0 表示探针执行完毕；日志内已复现缺陷，不能解释为产品全部通过。** 早先的 `app-probes-final.log` 等证据保留。
- 官网探针退出 0，结果见 `web-probes.log`；同样是完成探测，不是全部产品断言通过。
- 探针首次 restore 在沙箱中无法读取 NuGet 配置，授权执行相同构建后成功，后续 `--no-restore` 已在沙箱中完成。未更改产品依赖。
- 第一轮 Release 构建、221 PASS / 0 FAIL 和官网类型/API/安全检查结果仍引用[第一轮报告](2026-09-06-current-version-review.md)。功能源码未变，本轮未把这些旧执行结果冒充新一轮完整运行。

## 覆盖缺口和交接

- 实机歌词岛 Ctrl 操控/穿透、快速切歌及 seek、暂停保持、多播放器竞争、混合 DPI/多屏、Explorer 重启和睡眠恢复仍需真实 UI 场景；本轮有限离屏控件与 fake 环境不能替代它们。
- 上轮 Computer Use 无法定位透明歌词浮层；浏览器插件缺运行时文件。官网真实响应式渲染、生产缓存/路由以及真实数据库事务均未在本轮验收；本轮也没有重新宣称这些环境障碍已排除。
- 外部歌词源仅使用合成响应，未覆盖现时线上响应变化；未做整机长时间常驻和网络故障持续测试。
- 保留 `docs/desktop-core-domain-knowledge.md`、其他任务的托盘交接等现有 WIP；本轮仅新增/完成本报告、对应交接和本地 artifacts。未修改业务代码、正式测试断言、版本、发布候选或历史证据，未提交、推送、上传、发布。
- 建议顺序：D08/W03 的恢复与数据删除保护 → D01–D03 及第一轮缓存身份问题 → D04–D07/D09、W01–W02/W04–W05 → 基于各修复 SHA 的针对性回归和真实 UI 验收。接收方及 Evidence/Impact 见上表。
