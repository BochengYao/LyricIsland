# 任务交接：Architecture / 歌词坞小组件租约、设置回退与版本节奏

- 日期：2026-08-25
- 来源线程：歌词坞完成收口
- 当前工作区：含未提交的跨线程 WIP；本交接不授权整理、重置或提交它们。
- 当前本地候选：`publish/current`，`3.1.72-Beta`；`LyricHover.App.dll` SHA-256：`DC538FA9F271D7D96C3E78996BE971EC06B5D3EECE3E789C0F6CE2EA91E7FC91`。
- 当前候选来源不可由干净 Git SHA 单独追溯；因此它是本地验收包，不是外部发布或可审计发布候选。

## Task Contract

- Goal：供 Architecture 审查歌词坞对 Windows 任务栏小组件的状态租约、系统设置回退、外部变更检测与版本递增策略。
- Owner：Architecture。
- Allowed Write Scope：Architecture 可据此更新 ADR、共享决策或桌面契约；不要求此线程继续修改代码。
- Forbidden Scope：不得以此交接改变 Windows Shell 安全边界、绕过失败关闭策略，或将 `publish/current` 视为 Store/GitHub/官网发布。
- Dependencies：`OverlayPlacementSettings`、`LyricDockController`、`WidgetVisibilityLease`、`WindowsLyricDockEnvironment`、Windows 10/11 任务栏小组件设置。

## 已完成的行为

### 1. 小组件关闭采用“租约”而非永久接管

开启歌词坞会尝试读取并暂存原始小组件状态；成功后才临时关闭小组件。关闭歌词坞、应用正常退出和下一次启动恢复路径会尝试按租约恢复原始状态。安全空间不足或系统状态无法确认时，歌词坞保持 fail-closed，而不是强行覆盖任务栏。

相关实现：

- `LyricHover.App/LyricDock/WidgetVisibilityLease.cs`
- `LyricHover.App/LyricDock/LyricDockController.cs`
- `LyricHover.App/LyricDock/WindowsLyricDockEnvironment.cs`

### 2. 后台修改失败时的用户授权回退

当后台修改被系统或安全软件拦截时，应用不再只显示失败提示：用户可允许打开 Windows 任务栏设置，并由应用尝试定位和关闭“小组件”开关。若小组件本已关闭，不显示这条确认提示。设置页打开后的检测已改为轻量状态轮询，而非反复执行昂贵 UI 自动化扫描。

该回退的前提仍是用户确认；应用不得假定已打开设置即已完成关闭。设置开关实际变化后，控制器会复核状态并继续歌词坞流程；完成后会尝试关闭由应用打开的系统设置窗口。

### 3. 已有租约被用户外部改动后的再次关闭

此前租约已取得时，若用户在 Windows 设置中重新开启小组件，后续选择“通过设置关闭”可能只打开设置而不执行切换。`WidgetVisibilityLease.TryAcquireThroughSettingsUi` 现在会在已取得租约分支读取实时状态；仅当小组件已重新开启时才重新执行准备、设置 UI 切换、状态读取与刷新校验。

### 4. 设置状态与命名

产品展示统一使用“歌词坞”，不再使用“任务栏歌词”。歌词坞设置改变时即时同步运行时状态；Windows 小组件状态变化也会回写相关歌词坞有效状态。该线程没有改变原有的配置持久化格式或 Windows Shell 所需权限。

## Architecture 需要确认的决策

### A. 租约恢复与用户主动变更的优先级

建议确认以下规则：

```text
应用仅恢复由本次歌词坞租约临时更改的小组件状态；
用户在 Windows 设置中做出的后续主动变更优先于自动恢复。
```

当前实现已记录/复核状态，但“退出时是否无条件恢复最初状态，还是尊重租约期间用户新选择”应形成正式契约，避免未来控制器和恢复文件产生相反动作。

### B. UI 自动化回退的产品承诺边界

建议将 Windows 设置自动切换定位为 **best-effort、可验证后才继续** 的回退，不承诺在安全软件、语言变化、系统版本 UI 改版或企业策略限制下必然成功。失败时应保持歌词坞 fail-closed，并给出用户可完成的文字路径；不得以盲目重试替代确认。

### C. 公开版本号与本地构建号分离

用户已确认不应每次 UI 微调都递增公开版本。建议采用：

| 层级 | 规则 |
| --- | --- |
| 公开版本 `3.1.x-Beta` | 一组完整可验收的功能/修复批次完成才递增一次。 |
| `publish/current` 覆盖 | 可多次覆盖同一公开版本，记录构建时间、候选 SHA（存在时）和程序集哈希。 |
| 次版本 `3.2.0-Beta` | 仅用于新的一组完整用户能力，而非单一文案、配色或动画修复。 |
| 外部发布 | 另行要求干净候选 SHA、构建命令、哈希与渠道状态；本地包不等同外部发布。 |

建议 Architecture/Release 将此写入发布契约，并决定是否需要引入独立内部构建号。当前没有擅自新增持久化版本字段。

## 证据

- Release 构建：`dotnet build LyricHover.App\LyricHover.App.csproj -c Release --no-restore`，退出码 `0`，0 warning、0 error。
- 自动化测试：`LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1 dotnet run --no-restore --project LyricHover.Tests\LyricHover.Tests.csproj -c Release`，通过；测试项目仍输出既有 `CS0436` 链接源码/程序集类型冲突警告。
- 覆盖的行为测试包括：租约恢复、后台写入被阻断、已有租约后外部重新开启小组件时通过设置 UI 再次关闭、控制器响应 Windows 设置手动变更。
- 本地发布：`dotnet publish LyricHover.App\LyricHover.App.csproj -c Release --no-restore -r win-x64 --self-contained false -o publish\current`，完成。该命令未执行上传或商店提交。

## 风险、非目标与后续

| 证据 | 影响 | 建议 Owner |
| --- | --- | --- |
| 系统设置 UI 自动化依赖 Windows 结构、语言和安全策略 | 不能把“已打开设置”当作“已关闭小组件” | Architecture + LyricDock owner |
| 现有候选来自脏工作区 | 仅能用于本机验收，不能作为审计发布基线 | Release/Integration |
| 用户已验证多轮实机流程，仍未有完整 Windows 10/11、不同语言、安全软件组合矩阵 | 需要明确系统兼容性门槛与失败文案 | Quality + Desktop UI |

- 本交接没有修改 ADR、契约、Core、Windows 注册表权限或外部发布渠道。
- 推荐顺序：Architecture 确认 A/B/C；Quality 建立系统/语言/安全策略实机矩阵；Release 在干净 SHA 上生成下一正式候选。
