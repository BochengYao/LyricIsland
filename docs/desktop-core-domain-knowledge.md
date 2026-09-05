# Desktop Core 领域知识

> 维护者：Desktop Core 领域负责人。本文记录长期有效的桌面端核心知识，不记录单次任务过程、临时发布状态或界面方案。架构边界发生实质变化时，应同步给 Architecture 领域。

## 1. 领域目标与架构

Desktop Core 的目标是在不依赖视觉层的前提下，持续产出可信的“正在播放什么、处于什么状态、时间轴走到哪里、该显示哪一句歌词”的领域结果。

当前结构分为三部分：

1. `LyricHover.Core/Media`：平台无关的媒体领域模型与算法，包括播放器档案、会话选择、时间线、时间轴补偿、播放意图协调和稳定快照。
2. `LyricHover.Core/`：歌词解析、歌词源 Provider、歌词缓存、曲目身份、翻译轨、候选匹配、数据迁移与单实例等共享能力。
3. `LyricHover.App/Media`：Windows/SMTC 适配器。`SmTcMediaSessionService` 读取 Windows 媒体会话，转换为 `MediaSessionSnapshot`；`InstalledPlayerCatalog` 仅负责安装证据检测。`MainWindow` 当前是组合根，调用 Core 策略并把结果投影给岛屿和任务栏歌词。

`LyricHover.App/LyricDock` 是 Windows 任务栏歌词的环境控制与展示适配层：它处理安全位置、特性可用性和 Widgets 临时租约；歌词内容仍应来自同一 Core 播放/歌词状态，不能另建一套时间线或缓存。

目标边界是：Core 提供最小、稳定、可测试的 `Snapshot`、`State`、`Interface` 与 `Event`；UI 仅决定字体、布局、动画、窗口和交互呈现。当前 `MediaSessionSnapshot` 与 `LyricsPresentationSnapshot` 仍有可变属性，新增接口应优先采用不可变快照或防御性复制，避免 UI 读取内部可变集合。

## 2. 模块职责

| 模块 | 长期职责 |
| --- | --- |
| `MediaSessionSnapshot`、`MediaControlCapabilities` | 描述某一媒体会话的曲目信息、封面、播放状态、控制能力、位置、时长和活动时间。 |
| `PlayerProfileCatalog`、`SessionSelectionPolicy` | 识别支持的播放器并选定唯一会话：锁定播放器优先，其次 Windows 当前会话、最近活动的播放会话、最后是最近活动会话。 |
| `TimelineMetadataResolver`、`TimelineSampleCompensator`、`TimelineCoordinator` | 规范化 SMTC 时间线、补偿过期样本、在缺少可靠时间线时以单调时钟估算，并过滤小幅倒退抖动。 |
| `PlaybackIntentCoordinator` | 协调播放/暂停按钮的期望状态，收到实际会话状态后确认或在切换会话时取消，避免连续点击反转错乱。 |
| `SmTcMediaSessionService` | Windows SMTC 适配器：订阅会话、媒体属性、播放状态和时间线变化；以节流刷新生成快照，缓存封面，释放时解除订阅。 |
| `TrackIdentity`、`TrackIdentityCleaner`、`TrackTitleNormalizer` | 规范化标题、艺人、专辑和时长，清理播放器拼接标题、专辑尾缀、feat. 信息和可识别版本标记。 |
| `ILyricsClient`、`CompositeLyricsClient` 与 Provider | 获取同步歌词。默认编排优先保留含有效翻译的结果；自动来源链为 LrcLib、QQ 音乐、酷狗、网易云。单一 Provider 失败不能阻断后续来源。 |
| `LrcParser`、`LyricsPackageParser`、`TimedLyrics`、`LyricsDisplaySelector` | 解析 LRC、原文/翻译打包格式、按时间选择行和翻译行。时间戳空行、纯 `//` 翻译和不匹配的稀疏翻译不应生成可显示译文。 |
| `LyricsCache` | 对曲目身份和目标翻译语言隔离缓存；默认 64 MB，按最近使用时间淘汰；读取时允许有限时长漂移，避免播放器上报秒数微差导致缓存失效。 |
| `ProductDataDirectory`、`SingleInstanceGuard` | 维护数据目录迁移和单实例激活信号，属于产品兼容基础设施。 |
| `PlaybackVisibilityPolicy` | 根据播放、暂停、无媒体和设置编辑状态输出可见性决策；UI 负责实际隐藏/显示。 |

## 3. 关键技术选型

- **Windows SMTC，而非进程私有协议。** 使用 `GlobalSystemMediaTransportControlsSessionManager` 支持 Apple Music、QQ 音乐、网易云、酷狗、酷我和 Spotify 等可暴露 SMTC 的播放器；播放器适配基于 `SourceAppUserModelId` 档案，不把播放器私有实现写入歌词或 UI 层。
- **快照驱动。** SMTC 适配器读取一次会话后形成 `MediaSessionSnapshot`，选择、时间轴和歌词逻辑只使用模型数据；这样算法可在无 Windows 运行时的测试中验证。
- **单调时钟时间轴。** 可靠时间轴直接锚定；无可靠时间轴时只在播放中推进估算；暂停冻结；小幅倒退抖动不回退，大幅真实跳转允许校正。
- **Provider 链与容错。** 歌词来源是可替换的 `ILyricsClient`；任何单源网络、超时或 JSON 故障均降级到下一个来源，不让异常穿透为应用故障。
- **源原生翻译。** 翻译只能使用歌词来源返回的翻译轨；翻译语言作为包元数据与缓存键的一部分。没有对应目标语言的源翻译时返回原歌词，不调用第三方机器翻译服务。
- **保守的曲目匹配。** 标题、艺人、专辑、时长与版本标记共同参与评分。跨语言标题不能只凭“同艺人 + 接近时长”猜测；只有来源明确给出可验证别名，或未来有可靠曲目 ID/ISRC 时才可跨标题匹配。
- **安全的任务栏环境控制。** 任务栏歌词不注入 Explorer；Widgets 的 `TaskbarDa` 修改使用可恢复租约，记录原始 absent/0/1 状态，启动恢复残留租约，退出或禁用时精确恢复。无权限时保持 Widgets 可见并降级，而不是重试、提权或重启 Explorer。

## 4. 已解决且必须保留的行为

- SMTC 时间线会出现陈旧样本、缺失终止时间、重复位置和小幅向后跳动；时间轴协调器已分别处理补偿、估算、暂停冻结和抖动过滤。
- 会话可能同时存在，且用户可锁定播放器。选择顺序必须维持锁定优先、Windows 当前会话优先、播放与最近活动兜底，不能简单按枚举顺序取第一个。
- 歌词源可能返回仅时间戳、空文本或 `//` 占位翻译；这些内容不能导致空白翻译行，也不能把上一句翻译复用到下一句。
- 翻译轨可能比原歌词轨稍晚；可在受限容差内按最近原词对齐，但翻译必须仍以该原词为最近匹配，避免相邻句串行。
- 缓存键包含时长，播放器可能只产生秒级微差；读取允许受控的邻近秒数候选，写入后执行容量淘汰。
- 旧版本数据目录可能是 `LyricsIsland` 或 `AppleMusicDesktopLyrics`；首次启动优先迁移目录，发生占用或权限问题时保守复制缺失文件，不覆盖当前文件。
- 播放意图在会话切换时必须取消，不能把旧播放器的暂停/播放意图应用于新会话。

## 5. 已废弃或禁止重新引入的方案

- 不使用播放器专属 OCR、屏幕抓取、UI Automation 或网页 DOM 抓取来获得歌词；它们脆弱、不可测试且会绕过 Core 契约。
- 不依赖未公开的 Apple Music 歌词接口。Apple Music 的公开能力可用于有限元数据识别，但不是可承诺的逐行歌词来源。
- 不以艺人和时长作为跨语言曲名的唯一依据；该启发式曾把同艺人歌曲误配。遇到缺少可验证别名的情况，宁可返回未找到歌词。
- 不把翻译缺失交给第三方翻译服务或 UI 临时翻译；这会破坏来源、语言、缓存和隐私边界。
- 不在 UI 层复制歌词缓存、解析、选择或时间线计算；也不让 Core 依赖窗口位置、Taskbar、WPF、动画或视觉布局。
- 不用删除注册表值替代精确恢复 Widgets 状态，不写策略、不提权、不重启 Explorer。

## 6. 不可破坏的兼容性要求

- 用户数据当前目录为 `%LocalAppData%\\LyricHover`；必须保留从 `%LocalAppData%\\LyricsIsland` 和 `%LocalAppData%\\AppleMusicDesktopLyrics` 的迁移。
- Microsoft Store 包身份：`70643607.LyricIsland`。
- 应用标识：`LyricsIsland`。
- 单实例名称：`LyricsIsland.DesktopLyrics.SingleInstance`，并保留 `.Activate` 激活事件语义。
- 现有设置必须有默认值、升级路径和缺失值处理；涉及歌词来源、缓存容量、播放器选择、歌词偏移或任务栏歌词时必须说明迁移与回滚。
- 旧歌词缓存应可继续读取；新增翻译语言缓存只能增加语言后缀隔离，不能让默认缓存失效。
- 不降低已支持播放器的 SMTC 识别、控制能力、暂停/无媒体可见性策略或歌词同步精度。
- 任务栏歌词保留独立开关和显示屏选择；与顶部歌词岛可同时工作，但应共享同一播放、歌词和时间线真值。

## 7. 当前已知限制与技术债

- Windows SMTC 对外给出的是标题、艺人、专辑、封面、播放状态和时间轴等展示元数据；通常不提供 ISRC 或官方可复用的 Apple Music 歌词正文。因此本地化曲名和不同发行版本无法总是无歧义匹配。
- 歌词 Provider 是外部免费服务，覆盖、速率、字段和可用性不受本项目控制；自动链只能容错，不能保证所有歌曲都有同步歌词或翻译。
- 当前 SMTC 适配器因 `Dispatcher` 位于 App 层，`MainWindow` 仍承担较多编排与展示快照组装。后续拆分必须先冻结稳定 Core 服务接口，再迁移，不能直接大规模重构。
- 当前部分状态快照可变；领域演进应逐步改为不可变输出，但必须保持序列化、调用方和测试兼容。
- 任务栏 Lyrics Dock 的安全位置依赖 Windows 版本、DPI、任务栏自动隐藏/全屏状态和 Widgets 行为；不支持或无法安全放置时必须禁用或临时隐藏，而不能强行覆盖系统区域。

## 8. 后续开发原则

1. 先界定：播放器、歌词、时间线、缓存和共享模型属于 Desktop Core；窗口、视觉、动画、布局与网站属于其他领域。发现越界只输出 `Evidence + Impact + Suggested Owner`。
2. 先冻结最小契约，再改实现。新增播放器、状态、事件或歌词能力，必须先定义输入、输出、失败语义、并发语义、缓存影响和 UI 消费方式。
3. 算法必须可离线测试。会话选择、时间轴、歌词解析、歌词匹配和缓存策略不能只靠实机 UI 验收。
4. 精度优先于覆盖率。来源不明确、候选存在歧义、翻译不匹配或时间线不可靠时，保守降级，不显示错误歌词或错误状态。
5. 修改前分析影响：播放器档案、会话选择顺序、缓存键、语言隔离、旧目录迁移、单实例、设置默认值和任务栏租约均是潜在兼容面。
6. 脆弱启发式、结构性重构、设计冲突或需要 UI/网站范围的变更必须停机评审，不通过叠加例外规则“修到看起来能用”。
7. 验证至少覆盖直接 Core 单元/契约测试、相关桌面回归与必要的实机 SMTC 行为；构建或测试单独成功不代表完成。
8. 新的长期架构决策、已废弃方案或兼容边界变化，应同步到 Architecture 领域；本文随之更新，避免知识只存在于聊天记录。
