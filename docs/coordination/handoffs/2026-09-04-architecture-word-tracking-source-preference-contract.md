# 任务交接：Architecture / 逐字歌词跨域契约、源优先级与版本节奏

- 日期：2026-09-04
- 来源线程：逐字歌词追踪接入、翻译/强刷恢复、居中修复与 `3.2.32` 本地打包
- 交接目标：Project Coordination & Architecture
- Handoff Status：**Architecture Sync Required**
- 基线提交：`815444f9363d3567772296924731d472c205f649`
- 结果提交：未提交；当前为 `main` 上的混合未提交工作树。
- 当前本地候选：`publish/current`，`3.2.32-Beta`，framework-dependent `win-x64`。
- 候选哈希：App `D136F5B44EA22EDE421C0F9E0683526AC88613217966E4B5B1BEE9C57F3B2C1E`；Core `76515357B41B4B20425A1C9D52AF3098B7C113C066242AC1FFE44D82A9C39142`。

## Task Contract

- Goal：请 Architecture 审查并固化逐字歌词在 Core/App 之间的稳定契约、设置驱动的源选择规则、缓存升级/降级语义以及本地候选版本口径。
- Owner：Project Coordination & Architecture。
- Allowed Write Scope：ADR、`docs/api/DESKTOP_CONTRACTS.md`、共享决策、任务拆分和集成顺序。
- Forbidden Scope：本交接不授权 Architecture 顺手修改 Core/UI 实现，不授权清理混合工作树，也不把本地包提升为外部发布。
- Dependencies：Core Handoff、UI Handoff、Quality/Integration 实机验收、Release 对版本和渠道状态的核验。

## 当前跨域实现

```text
OverlayPlacementSettings
  ├─ IslandWordTrackingEnabled
  └─ LyricDockWordTrackingEnabled
             │ 任一开启
             ▼
WordTimedPreferredLyricsClient
  ├─ QQ QRC / NetEase YRC（可验证逐字候选）
  └─ 用户现有歌词源顺序（普通歌词与翻译回退）
             │ 原始歌词包缓存
             ▼
LyricsPackageParser
  ├─ TimedLyrics → LyricLine + optional LyricWord[]
  └─ Translation → 现有行级 LRC 轨
             │ 同一播放有效位置
             ▼
LyricsPresentationSnapshot / IslandRenderState
  ├─ 歌词岛按自己的开关消费
  └─ 歌词坞按自己的开关消费
```

UI 没有新增解析、缓存或第二时间线；Core 不知道具体展示表面，只暴露可选逐字数据。旧 LRC 缓存仍是合法输入，启用逐字时由 App 触发一次升级尝试。

## Architecture 需要确认的决策

### A. `LyricLine.Words` 是否进入稳定桌面契约

建议确认：

```text
LyricLine 的行时间和 Text 是稳定基础语义；Words 是可选增强数据。
Words 缺失、无效或提供方失败时，消费者必须继续显示 Text，不得视为无歌词。
```

理由：这能让旧缓存、普通 LRC、未来其他来源和现有显示逻辑保持兼容，也避免 UI 根据字符串重新推导逐字时间。

### B. 展示设置是否可以影响获取优先级

当前规则是“歌词岛或歌词坞任一逐字开关开启，就临时优先 QQ/网易逐字候选；找不到后恢复用户配置的常规歌词源优先级”。请确认这是产品级规则，而不是 UI 偶然改变 Core 排序。

建议明确两个层级：

| 层级 | 建议规则 |
| --- | --- |
| 能力获取 | 任一消费者请求逐字能力时，允许先尝试支持该能力的来源。 |
| 内容回退 | 没有可验证逐字候选时，必须恢复用户源顺序、翻译规则和可用普通歌词。 |

### C. 缓存升级失败时的保留规则

建议确认：旧 LRC 缓存不是失效缓存。启用逐字时可刷新一次，但网络失败、空结果或解析失败不得清空现有歌词；只有获得非空、可解析包才替换缓存。强制刷新也遵循同一保留规则。

### D. 翻译边界

当前只有原文主轨逐字高亮，翻译保持提供方原生的行级 LRC。建议写入契约，避免后续 UI 将翻译按字数均匀分割，或 Core 合成不存在的翻译词时间。

### E. 帧级显示责任

建议确认：Core 负责词片段数据和给定播放位置的确定性进度；UI 可在可靠播放状态下基于同一有效位置做短期逐帧投影，但暂停、跳转、切歌和新快照必须重新锚定。UI 不得建立独立媒体时间线。

### F. 版本节奏与 `3.2.32`

- 用户此前明确：只有增加新功能或完成一次完整修复才增加版本，小修尝试不重复递增。
- 居中小修复完成后曾覆盖同一 `3.1.74-Beta` 候选，没有加版本。
- 用户随后明确要求打包为 `3.2.32`，版本源已直接设为 `3.2.32`，没有通过多次补丁递增制造中间候选。
- `publish/current` 已替换为 `3.2.32-Beta`；旧 `3.1.74` 归档。本地包仍不是 GitHub、Store、官网或其他外部渠道发布。

请 Architecture/Release 确认是否把“显式指定跨 minor 版本”的流程加入发布脚本。目前 `publish.ps1` 只支持当前补丁 `+1` 或 `-KeepVersion`，本次使用了等价的测试、构建、暂存、旧包实际版本归档和原子目录替换流程。

## 验证与候选状态

| Evidence | Result |
| --- | --- |
| Core/UI 回归 | 执行的产品回归全部 PASS；覆盖 QRC/YRC、普通 LRC 回退、翻译、独立开关、强刷、逐帧裁切和当前句居中。 |
| `win-x64` Release build | 成功，0 warning、0 error。 |
| `publish/current` | ProductVersion `3.2.32-Beta`；App/Core 哈希与构建输出核对。 |
| 运行状态 | 当前包未启动。 |
| Git 可追溯性 | 基线 SHA 可识别，但结果存在未提交混合 WIP；没有结果 SHA。 |
| 外部渠道 | 未上传、未提交、未审核、未发布。 |

## Architecture 风险清单

| Evidence | Impact | Suggested Owner |
| --- | --- | --- |
| 设置决定是否优先尝试特定提供方 | Core 获取策略与 UI 产品设置形成跨域依赖，需要正式契约 | Architecture + Core |
| QRC 解密和 YRC 字段属于服务实现细节 | 服务变化时必须降级，不可让 UI 显示“未找到同步歌词”代替已有 LRC | Core |
| UI 使用帧级短期投影 | 若边界未写清，未来可能出现 Core/UI 双时间线和暂停漂移 | Architecture + UI |
| 两个开关默认 `true` 且旧设置缺字段时由默认初始化补齐 | 这是设置升级语义，应进入桌面契约并由 UI 保持 Apply/Cancel 边界 | Architecture + UI |
| 当前候选来自脏工作树 | 适合本机验收，不适合正式发布或直接宣称已集成 | Integration + Release |
| 尚缺 QQ/网易/LRC-only 的 `3.2.32` 在线实机证据 | 自动化不能证明提供方实时响应和完整视觉体验 | Quality + Core + UI |

## 建议集成顺序

1. Core 审核逐字模型、QRC/YRC 失败边界和源回退，给出接受或修订意见。
2. Architecture 依据 Core 结论更新桌面契约/ADR，确认 A–F。
3. UI 在 `publish/current` 上完成独立开关、强刷、暂停/跳转、翻译、居中和 DPI 实机矩阵。
4. Quality 在干净集成分支重跑完整桌面回归，核对候选哈希与结果 SHA。
5. Release 只有获得用户明确授权后才处理 GitHub、Store 或官网渠道；本交接不包含外部发布授权。

## 回滚点

- 架构层可将逐字能力定义为可选实验增强，要求 UI 默认关闭并保留普通 LRC；但这会改变当前默认开启的产品语义，必须由用户确认。
- 实现回滚必须成对处理 Core 可选词片段与 UI 消费，不得留下无法满足的设置承诺。
- 版本回滚只能替换本地候选并记录真实产物版本，不得重写已有归档或对外宣称渠道回退。
