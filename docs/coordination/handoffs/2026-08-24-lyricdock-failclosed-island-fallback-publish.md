# 任务交接：LyricDock fail-closed 与 Island 持久化回退

- 日期：2026-08-24
- 任务线程：Desktop UI / Release Integration
- 基线提交：`4decc8e340dfd76d942794bdf128ce8c6d224157`
- 结果提交：未提交；工作区含其他线程 WIP，本交接不将其归属为本任务改动。
- 允许修改范围：`LyricHover.App/MainWindow.xaml.cs`、`LyricHover.Tests/Program.cs`、`tools/publish-next-version.ps1`、`Directory.Build.props` 与根目录 `publish/current` 本地候选。

## Task Contract

- Goal：将“至少一个歌词展示表面可用”落实为持久化不变量；LyricDock 不可靠时 fail-closed，并确定性回退至 `IslandEnabled=true`；生成已验证的 `publish/current` 候选。
- Owner：Desktop UI / Release Integration。
- Allowed Write Scope：App 设置运行时同步、对应回归测试、候选打包门禁和本地候选输出。
- Forbidden Scope：不下沉共享契约或 Core；不修改 LyricDock Shell 探测/租约语义；不提交、上传或发布到 GitHub、Store 或官网。
- Dependencies：`OverlayPlacementSettings.Normalize()`、`SettingsRuntimeStateCoordinator`、LyricDock `Configure(...)` fail-closed 结果、`publish.ps1 -NoLaunch`。
- Design Constraints：Dock 失败必须关闭 Dock；不得为满足展示约束重开 Dock；有效设置必须经 `Normalize()` 后持久化；候选脚本不得在持有发布互斥锁时递归运行会取得同一锁的事务夹具。
- Definition of Done：运行时失败后的 effective settings 为 `LyricDockEnabled=false`、`IslandEnabled=true`；重启后仍保持；完整候选门禁通过并替换 `publish/current`，且哈希与 Release 输出一致。
- Verification：定向设置回归、独立发布事务夹具、完整候选脚本、版本/哈希/残留 staging 检查。
- Handoff：交给 Quality/Integration 做真实 Windows 11 Widgets/UIA 失败场景与设置窗口实机验收；Architecture 决定是否下沉共享契约。

## 已完成

1. `SynchronizePlacementSettingsRuntime(...)` 明确仅使用传入的 `runtimeSettings` 读取、配置和回写状态。Dock 配置失败时，先令 `runtimeSettings.LyricDockEnabled=false`；协调器随后执行 `Normalize()`，双关闭状态确定性修复为 `IslandEnabled=true` 并原子持久化。
2. 将设置回归用例命名为“LyricDock safety failure falls back to Island and persists the effective state”，覆盖：Dock 从开启变为安全关闭、Island 自动开启、保存后再次加载仍为 Island 开启。
3. 修复候选打包门禁的互斥锁递归：`publish-next-version.ps1` 持有 `LyricsIsland.PublishNextVersion` 时设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1`，完整套件跳过会启动第二个候选生成器的事务夹具；夹具仍在锁外单独执行。
4. 先按 `win-x64` 还原，随后以 `publish.ps1 -NoLaunch` 生成并切换本地候选 `v3.1.48 Beta`。

## 变更文件

- `LyricHover.App/MainWindow.xaml.cs`：运行时同步只使用 effective settings 参数，保留 Dock fail-closed 后的归一化回退。
- `LyricHover.Tests/Program.cs`：回退测试语义化命名；增加发布脚本持锁时的事务夹具跳过开关。
- `tools/publish-next-version.ps1`：在完整候选测试前设置跳过开关，避免发布锁递归。
- `Directory.Build.props`：候选版本由脚本从 `3.1.47` 递增至 `3.1.48`。

## 验证

| 命令 / 检查 | 结果 |
| --- | --- |
| `dotnet build LyricHover.sln -c Release` | 成功；0 error。测试项目存在 158 条既有 `CS0436` 重复类型警告。 |
| `dotnet run --no-build --configuration Release --project LyricHover.Tests -- --settings-runtime-state-fixture` | 4/4 PASS，包括 Dock 安全失败回退、持久化与重启恢复。 |
| `dotnet run --no-build --configuration Release --project LyricHover.Tests -- --release-version-fixture` | PASS；事务、回滚、并发互斥语义在锁外验证。 |
| `dotnet restore LyricHover.App\\LyricHover.App.csproj --runtime win-x64` | 成功；解决 `NETSDK1047` 运行时资产缺失。 |
| `publish.ps1 -NoLaunch` | 成功；完整套件通过，`win-x64` Release build 0 warning / 0 error，输出“发布完成：v3.1.48 Beta”。 |
| `publish/current` 产物核验 | `ProductVersion=3.1.48-Beta`，`FileVersion=3.1.48.0`；DLL SHA-256 为 `EA7570A2D9B1126EB45B857CD558FA34EFA8983EF7DDC772DAFBFCCF8397A2B5`，与 `LyricHover.App/bin/Release/netcoreapp3.1/win-x64` 一致；`publish/staging-v3.1.48-Beta` 不存在。 |

## 未修改 / 非目标

- 未迁移 `OverlayPlacementSettings.Normalize()` 到 `LyricHover.Core` 或共享桌面契约；该归属仍由 Architecture 评审。
- 未改变 LyricDock 的 Widgets 探测、注册表恢复租约、Explorer/UIA 或安全失败策略。
- 未获得真实用户桌面上“Island 关闭 + Dock 配置失败”的窗口截图、UIA 证据或设备矩阵结果。
- `publish/current` 是本地 framework-dependent 候选，非 MSIX、GitHub Release、Store 上传/提交/发布或官网生产发布。

## 风险与后续

- 已知限制：自动测试验证的是协调器与假环境语义，不能替代真实 Windows 11 Widgets 位置、租约恢复及窗口首帧验收。
- Evidence + Impact + Suggested Owner：真实 Dock 失败后 Island 显示与设置窗口回写，需要 Quality/Integration 在 Windows 11 实机验证；Architecture 评估是否把持久化不变量写入共享契约。
- 回滚点：代码审查基线为 `4decc8e340dfd76d942794bdf128ce8c6d224157`；本地候选已由脚本将先前 current 移入 `publish/archive/`，不得用 reset/clean 清理其他 WIP。
