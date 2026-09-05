# 任务交接：Architecture / 歌词岛与歌词坞表面可用性约束

- 日期：2026-08-24
- 任务线程：项目统筹与架构（设计评审输入）
- 基线提交：`4decc8e340dfd76d942794bdf128ce8c6d224157`
- 结果提交：未提交；此记录不修改共享架构或决策文件。
- 关联实现：`LyricHover.App/OverlayPlacementSettings.cs`、`LyricHover.App/PlacementSettingsWindow.xaml.cs`、`LyricHover.Tests/Program.cs`。
- 关联本地候选包：`publish/current`，`3.1.47-Beta`；非 GitHub、Store 或官网生产发布。

## Task Contract

- Goal：将“歌词岛与歌词坞至少一个保持开启”明确为设置域约束，并确认其与现有 `UI Draft → Runtime State → Persist` 模型兼容。
- Owner：项目统筹与架构。
- Allowed Write Scope：评审本交接；如接受，按架构流程更新 ADR 或共享决策文档。
- Forbidden Scope：本交接不授权修改 Desktop Core、Shell 安全策略、发布渠道或现有其他线程 WIP。
- Dependencies：已接受的设置运行时状态模型、`OverlayPlacementSettings.Normalize()`、设置运行时协调器的 effective settings 回传。
- Design Constraints：配置文件不得持久化为“双关闭”；UI 自动修正不得绕过运行时/持久化提交边界；LyricDock 的安全失败必须继续 fail-closed。
- Definition of Done：架构线程确认这是长期产品不变量还是 UI 层策略；若是长期不变量，确定唯一归一化位置、外部失败处理和回滚/迁移语义。
- Verification：审查实现路径、针对性测试、真实 UI 验收和下一次设置重载验证。
- Handoff：Architecture 审查结论交回 Desktop UI、Quality/Integration 与 Release。

## 现状与变更动机

设置已经采用三层状态模型：UI 只编辑 detached draft，运行时同步可对 draft 施加环境约束并返回 effective settings，随后持久化 effective settings。用户新增产品规则：顶部歌词岛和歌词坞不能同时关闭；尝试关闭最后一个已开启表面时，应自动开启另一个表面。

没有此规则时，配置文件可保存 `IslandEnabled=false` 与 `LyricDockEnabled=false`，应用可启动但没有可见歌词表面，且用户难以恢复。

## 当前实现与状态流

```text
用户切换 Island / LyricDock
  → PlacementSettingsWindow 的 UI guard
  → 至少一个为 true 的 draft
  → SettingsRuntimeStateCoordinator / MainWindow 运行时同步
  → effective settings
  → Normalize() 双关闭兜底
  → 原子持久化
```

1. UI guard：若用户关闭的动作会使两项均为 false，则切换另一项为 true。该路径满足“自动开启另一个”的具体交互要求。
2. 归一化兜底：`OverlayPlacementSettings.Normalize()` 遇到双关闭时设 `IslandEnabled=true`。这覆盖旧 JSON、手工编辑配置和任何绕过设置窗口的输入；没有“用户刚关闭哪一项”的上下文时，歌词岛是确定且兼容的恢复目标。
3. effective settings：设置窗口继续以运行时返回的有效快照更新开关；该规则没有把 UI 控件当作长期真值。

## 需要 Architecture 裁决的点

### A. 是否升格为长期配置不变量

建议：**接受为持久化模型的不变量**，而不是只留在 WPF 事件处理器。理由是双关闭可来自旧文件、手工修改、迁移、故障回写或未来入口；只在 UI 防护会留下非法配置状态。

建议形式：

```text
IslandEnabled || LyricDockEnabled == true
```

规范化应保持幂等，且在缺少交互来源信息时固定恢复 `IslandEnabled=true`。

### B. LyricDock fail-closed 的优先级

需要明确：Windows 11 环境探测或 Widgets 租约失败时，LyricDock 必须关闭并恢复系统状态。若此时歌词岛也关闭，配置不变量会使歌词岛成为唯一恢复目标；不得为了满足“至少一个”而重新强行打开不安全的 LyricDock。

建议：**安全失败优先，回退到歌词岛**。若歌词岛运行时也无法显示，应在后续 Feature Task 设计用户可见的失败反馈，而不是违反 LyricDock fail-closed。

### C. 约束的最终归属

当前 `Normalize()` 在 App 程序集。它保证现有持久化路径一致，但 Architecture 可选择后续将纯配置不变量下沉为共享/核心可测试模型；本次不应在没有契约评审时跨域搬迁。

建议：当前保留在 `OverlayPlacementSettings.Normalize()`，并在 `DESKTOP_CONTRACTS.md` 或 ADR 中写清“设置持久化兼容约束”，由未来 Core/UI 协作任务评估是否下沉。

## 证据与影响

| 证据 | 影响 | 建议 Owner |
| --- | --- | --- |
| 既有设置模型要求 UI draft、运行时状态和 committed config 分离 | 约束不能只存在于模板或单一点击回调 | Architecture + Desktop UI |
| `Normalize()` 是旧设置缺失值与外部输入的统一入口 | 可防止双关闭状态重新写入配置 | Desktop UI，Architecture 审查 |
| LyricDock 是 Windows Shell 安全敏感能力且要求 fail-closed | 不变量不能迫使不安全的 LyricDock 重新启用 | Architecture + LyricDock owner |
| 本地候选包和完整测试通过 | 构建验证存在；不等于真实 Shell 或窗口交互验收 | Quality/Integration + Desktop UI |

## 验证证据

- Release 应用构建：`dotnet build LyricHover.App\LyricHover.App.csproj -c Release --no-restore`，退出码 `0`，0 warning、0 error。
- 完整候选脚本：`publish.ps1 -NoLaunch`，退出码 `0`，完整桌面测试输出 PASS 并生成 `3.1.47-Beta` 本地候选包。
- 测试调整覆盖：运行时 apply 后若 draft 双关闭，effective settings 与持久化 settings 均恢复歌词岛开启；重启恢复同样遵循该值。
- 未完成证据：未取得真实用户桌面下的开关首帧、关闭动画、LyricDock 安全失败后回退歌词岛的截图或实机矩阵证据。

## 未修改 / 非目标

- 本记录未修改 `docs/coordination/04-DECISIONS.md`、`docs/api/DESKTOP_CONTRACTS.md` 或 ADR；这些均属 Architecture 的共享文档权限。
- 未修改 LyricDock 的探测、Widgets 租约、Explorer 交互或任务栏定位；安全故障语义维持原状。
- 未把 `publish/current` 表述为外部发布。

## 建议决策与后续顺序

1. Architecture 确认 A/B/C 的建议或给出替代归属。
2. Desktop UI 完成真实窗口的首开、双向动画、最后一个表面关闭和重启验收。
3. Quality/Integration 为“外部 LyricDock fail-closed 且 Island 关闭”的组合场景补一条可执行测试或列为实机门禁。
4. 仅在 UI 和质量证据齐备后，由 Release 线程判断本地候选包是否具备下一阶段资格。

## 回滚点

`4decc8e340dfd76d942794bdf128ce8c6d224157`。该回滚点只用于实现审查；不得以 reset/clean 清理其他线程的未提交工作。
