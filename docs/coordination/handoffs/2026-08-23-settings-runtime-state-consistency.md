# Settings Runtime State Consistency Report

- 日期：2026-08-23
- 任务线程：Feature / Settings Runtime State Consistency
- 基线提交：`5d48dd1f7c777b0d8762d29180d0bec40a918884`
- 结果提交：`6ffe637`（设置状态协调实现与回归测试）
- 允许修改范围：`LyricHover.App/` 的设置协调、窗口与模块运行状态；`LyricHover.Tests/` 的针对性回归；本交接文件
- 验证方式：Release 构建、同一构建产物的桌面测试、设置 Apply/Cancel/关闭/重载状态测试及可执行的窗口恢复验证

## 当前问题原因

1. `MainWindow.placementSettings` 原先既是持久化配置对象，也是运行时直接修改的对象；`settingsStore.Save(...)` 分散在 Apply、拖动、教程、LyricDock 故障和布局编辑路径，缺少统一提交边界。
2. 设置窗口虽从 `DeepClone()` 开始编辑，但布局预览会提前修改 `placementSettings.IslandLayouts.Mode`；Apply 前 `SaveLayoutEditing()` 还会先写文件，导致 UI 草稿、运行时预览和长期配置混在同一事务中。
3. Apply 回调原为 `Action<OverlayPlacementSettings>`，运行时可因环境约束强制修正值（例如 LyricDock 启用失败），但 UI 没有正式契约取得“最终有效状态”。
4. 鼠标避让暂停由设置窗口直接调用 MainWindow 回调；语言选择会即时修改全局语言服务，Cancel/关闭没有统一恢复已提交值。
5. LyricDock 运行时故障会持久化关闭，但已经打开的设置窗口可能继续显示开启，形成配置、运行时与 UI 三方不一致。
6. `IslandEnabled` 缺少完整的长期配置键、UI 草稿和运行时显隐闭环；旧设置文件也需要缺失值默认为开启。

## 状态模型变化

- 配置单一事实来源：`SettingsRuntimeStateCoordinator.Current`（仅 App 程序集内部可访问）持有当前 committed settings；所有文件写入统一经过协调器。
- UI 临时状态：设置窗口只接收 `CreateEditSnapshot()` 的 detached draft。编辑、Cancel 和普通关闭不会修改 committed settings 或设置文件。
- Apply：协调器克隆并规范化 draft，调用 MainWindow 的运行时同步，再保存运行时修正后的 effective settings，并把该快照返回给 UI。LyricDock 强制关闭等回落会同时反映到文件、运行时和开关。
- 运行时变化：教程标记、拖动落点和 LyricDock 外部禁用通过协调器的运行时提交接口持久化，不再直接调用 store。
- 布局预览：`LayoutEditSession` 仍是允许的临时运行时预览，但不再提前改 `IslandLayouts.Mode` 或单独保存；只有总 Apply 提交捕获到的布局草稿。Cancel/关闭恢复 committed layout。
- 窗口生命周期：MainWindow 拥有“设置窗口打开时暂停鼠标避让”的运行时状态；窗口只管理自己的草稿与预览。即时语言预览在关闭时恢复最后一次成功 Apply 的语言。
- IslandEnabled：默认 `true`，Apply 后关闭立即隐藏、开启按当前播放/等待状态恢复；设置窗口或教程打开不能绕过关闭状态。

## 修改文件

- `LyricHover.App/SettingsRuntimeStateCoordinator.cs`：新增配置提交、运行时同步、草稿隔离和临时运行快照协调器。
- `LyricHover.App/OverlayPlacementSettings.cs`：补齐 `Language` / `IslandEnabled` 持久化模型、默认值、规范化与兼容加载检查。
- `LyricHover.App/MainWindow.xaml.cs`：统一经协调器读写；实现 IslandEnabled 运行时显隐；收口 Apply、布局预览、鼠标避让暂停和 LyricDock 外部禁用。
- `LyricHover.App/PlacementSettingsWindow.xaml`：在现有歌词设置区加入顶部歌词岛开关，未调整整体视觉系统。
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`：Apply 接收 effective snapshot；Cancel/关闭仅丢弃 draft；外部 LyricDock 禁用与语言预览恢复一致。
- `LyricHover.Tests/Program.cs`：新增 4 项行为/生命周期回归及 `--settings-runtime-state-fixture` 定向入口。

## 测试结果

- Release 构建：`dotnet build LyricHover.sln -c Release --no-restore`，退出码 `0`，0 error；157 个既有 `CS0436` 警告来自测试项目同时链接 LyricDock 源文件并引用 App 程序集。
- 定向测试：`dotnet run --project LyricHover.Tests -c Release --no-build -- --settings-runtime-state-fixture`，退出码 `0`，4/4 PASS：
  - 修改草稿后 Cancel 语义：committed 与文件均不变。
  - Apply：运行时收到新值，运行时强制修正后的 effective state 被持久化并返回 UI。
  - 重启：新协调器从设置文件恢复 IslandEnabled=false。
  - 窗口关闭：鼠标避让暂停归 MainWindow 生命周期所有；语言预览恢复；布局模式不提前污染 committed settings。
- 完整桌面测试：按“两次相同方法上限”运行两次；新增 4 项均 PASS，两次均在既有 `support developer page exposes Pro and free support actions` 静态断言处出现相同失败。该断言检查 Support 面板布局/滚动，本任务未修改 Support 区域，因此分类为基线现状，不为绿灯改动无关业务。
- `git diff --check`：通过。
- 手动 UI：未在本线程操纵真实用户配置或真实桌面窗口；自动测试覆盖保存、取消、重载和关闭状态，真实窗口视觉/交互仍列为审查时手动验收项。

## 新增长期约束

1. 设置文件的 committed settings 是长期配置唯一事实；UI 不得持有或修改协调器的内部 Current 实例。
2. 新设置必须同时定义默认值/缺失值行为、UI draft 字段、MainWindow 运行时同步和 Apply/Cancel/重启测试。
3. Apply 必须返回运行时约束后的 effective settings；不得假定用户 draft 一定能原样生效。
4. 所有 `OverlaySettingsStore.Save` 调用应留在设置协调器内；运行时故障或交互提交使用协调器接口。
5. 临时预览必须有清晰的 Begin/Apply-or-Cancel/Close 生命周期，不得在 Apply 前写入 committed settings。
6. 设置窗口可预览自身外观或语言，但关闭时必须恢复最后一次已提交状态；不得用隐藏控件或控件可见性充当业务状态。
7. 鼠标避让暂停属于 MainWindow 的设置会话运行时状态；设置页只触发窗口生命周期，不直接控制底层避让业务。

## 同步需求

- Desktop Core：不需要 Core 代码同步；需要由桌面契约维护者知悉新增可选持久化键及默认/缺失值兼容行为。
- Desktop UI & Interaction：需要。由该线程审查并合并 `6ffe637`，再在当前设置页 WIP 上解决重叠 UI 文件的集成冲突。
- Architecture：需要。审查通过后应把“committed / runtime / UI draft 三层模型”和 Apply effective-state 约束写入正式架构文档；本线程不直接修改项目统筹所有的共享架构文档。

## 未修改 / 非目标

- 不修改设置页视觉设计、圆角系统、整体 UI 框架或歌词同步逻辑。
- 不修改 `LyricHover.Core/`、发布文件、共享治理文档或其他线程 WIP。

## 风险与后续

- 已知限制：根工作树存在未提交的设置页、IslandEnabled、LyricDock 和视觉 WIP；本实现基于干净 `5d48dd1`，不得用整文件覆盖方式合并。真实 WPF 窗口的点击、关闭与恢复需在集成后的当前 UI 上手动验收。
- 交接目标：Desktop UI & Interaction 审查；随后交项目统筹同步 Architecture 约束，并知会 Desktop Core 契约维护者。
- 回滚点：`5d48dd1f7c777b0d8762d29180d0bec40a918884`
