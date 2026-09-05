# 会话总结：省电模式实现（v3.1.16）与 publish\current 目录膨胀诊断清理

- 日期：2026-08-22 ~ 2026-08-23
- 会话背景：本会话延续自上一会话（曾基于 2.1.9 实现省电模式）。本会话完成了两件事：① 在 v3.0.16 基线上重新实现省电模式（版本号 3.1.16）；② 诊断并清理 `publish\current` 目录膨胀问题

---

## 第一部分：省电模式实现（基于 v3.0.16）

### 1. 需求与背景

- 省电模式目标：降低后台资源占用——禁用全部动画、定时器降频，仅保留歌词显示
- 用户关键纠正：**必须在 3.0.16 基线上实现（而非旧的 2.1.9），版本号定为 3.1.16**——因为用户已将 v3-taskbar-lyrics 分支（真实任务栏歌词功能）合并到 main，旧的 2.1.9 实现已被合并覆盖

### 2. 架构适配决策

- 原计划中的 `ILyricDockBridge` 桥接口和 `LyricDisplayMode` 显示模式枚举**不再需要**：v3.0.16 主线已有真实歌词坞实现（`LyricDockEnabled` 布尔设置 + `LyricDockController`）
- 省电模式简化为单一布尔开关 `EnablePowerSavingMode`，与任务栏歌词开关并存而非互斥
- 动画控制采用分层传递：`MainWindow.ApplyPowerSavingState()` → `IslandModuleHost.SetAnimationsEnabled()` → 各模块视图的 `AnimationsEnabled` 属性

### 3. 修改清单（9 个文件，+190/-6）

| 文件 | 改动内容 |
|---|---|
| `LyricHover.App/OverlayPlacementSettings.cs` | 新增 `EnablePowerSavingMode` 属性 + Load() 差异检测；恢复被合并丢失的 `Language` 属性与 `AppLanguagePreference` 枚举 |
| `LyricHover.App/MainWindow.xaml.cs` | 定时器常量（主刷新 250ms→1000ms、悬停接近 40ms→500ms）、`powerSavingActive` 标志、`ApplyPowerSavingState()` 调度方法、位置/尺寸动画与悬停辉光跳过 |
| `LyricHover.App/Modules/IslandModuleHost.xaml.cs` | `SetAnimationsEnabled()`、ApplyLayout 时向子模块传递动画开关、模块渐显/重排动画跳过 |
| `LyricHover.App/Modules/LyricsModuleView.xaml.cs` | `AnimationsEnabled` 属性、歌词切换直接应用（跳过过渡动画）、跑马灯跳过 |
| `LyricHover.App/Modules/PlaybackControlsModuleView.xaml.cs` | `AnimationsEnabled` 属性、四个鼠标事件（悬停/按下动效）跳过 |
| `LyricHover.App/PlacementSettingsWindow.xaml` | 新增"省电模式"设置行（CheckBox + 说明文本），保留用户已有的任务栏歌词设置区域 |
| `LyricHover.App/PlacementSettingsWindow.xaml.cs` | 初始化、`CaptureSettings()`、`AttachSettingsChangeHandlers()` 三处接入（遵循现有脏状态 + 应用按钮模式） |
| `LyricHover.Tests/Program.cs` | 新增源码扫描测试 `PowerSavingModeSchemaDefaultsToDisabled` |
| `Directory.Build.props` | 版本号 3.0.16 → 3.1.16 |

### 4. 错误与修复

- **构建失败：9 个 `AppLanguagePreference` 未定义错误**。根因是合并提交丢失了本地化功能的枚举定义；通过 `git grep` 在 stash 提交 6a2b58e 中找到并恢复了枚举（System/SimplifiedChinese/TraditionalChinese/English/Japanese）、`Language` 属性、Normalize() 校验与 Load() 差异检测

### 5. 验证结果

- 构建：0 错误、0 警告
- 测试：185 通过 / 13 失败；新增测试通过；13 个失败逐一核实均为**预存问题**（9 个为沙箱下 `C:\WINDOWS\TEMP` 权限被拦截，4 个为合并造成的字符串漂移），与本次改动无关
- 项目架构记忆已更新（修正旧的 2.1.9 桥接设计描述为 v3.1.16 实现方式）

---

## 第二部分：publish\current 目录膨胀诊断与清理

### 1. 用户问题

用户发现 `D:\AppleMusicDesktopLyrics\publish\current` 文件夹突然变得非常杂乱，询问原因。

### 2. 诊断结论

**根因**：`publish\current` 在 2026-08-22 凌晨 02:45 被一次**手动自包含发布（self-contained）**覆盖，整个 .NET 3.1 运行时被灌入该目录。这不是官方发布脚本产生的结果。

| 项目 | 历史正常状态（所有 archive / Beta） | 出问题的 current |
|---|---|---|
| 文件数 | 10 个 | 502 个 |
| 大小 | 32 MB | 213 MB |
| 发布方式 | 框架依赖 | 自包含 |

关键证据：

1. `runtimeconfig.json` 从正常的 `"framework"` 变为 `"includedFrameworks"`（`Microsoft.NETCore.App 3.1.32` + `Microsoft.WindowsDesktop.App 3.1.32`）
2. 目录中出现 `coreclr.dll` / `hostfxr.dll` / `hostpolicy.dll`（自包含发布的铁证）
3. 约 200 个 `System.*.dll`、`api-ms-win-*.dll`、`ucrtbase.dll`、`wpfgfx_cor3.dll` 及 14 个语言目录（`cs`、`de`、`zh-Hans` 等）
4. `current` 根目录的 `LyricHover.App.dll` 版本为 3.0.16.0，写入时间 2026-08-22 02:45
5. 官方发布脚本 `tools\publish-next-version.ps1` 第 109 行明确使用 `--self-contained false`，绝不会产出此结果——可判定是绕开脚本的手动发布（很可能带 `-r win-x64` 但漏加 `--self-contained false`）

附带发现：

- `.gitignore` 第 3 行含 `publish/`，整个发布目录被忽略，**git 仓库未被污染**（`git status` 干净）
- `current\staging\` 内残留一份旧的框架依赖产物（版本 3.0.2.0），也是手动操作的遗留物

### 3. 清理过程

用户选择「手动清理并重新生成（不改版本号）」方案。

**已完成**：

1. 生成干净产物：
   ```
   dotnet publish --configuration Release --runtime win-x64 --self-contained false --output publish\_clean-staging LyricHover.App\LyricHover.App.csproj
   ```
2. 验证通过：`publish\_clean-staging` 为 11 个文件 / 32 MB，`LyricHover.App.dll` 版本 **3.1.16.0**（当时源码版本），`runtimeconfig.json` 为正确的 `"framework"` 模式，无运行时文件（仅 `System.Management.dll` 为应用自身 NuGet 依赖，属正常）

**被阻塞（沙箱限制）**：

- `LyricHover.App.exe`（PID 28112）正从 `publish\current` 运行，锁定了文件
- 沙箱基础设施层拦截了 `Stop-Process` 和 `Remove-Item -Recurse`，均报 `GetNamedSecurityInfoW 失败: 5`（权限拒绝）

**移交给用户的操作**（退出托盘中的应用后在自己的终端执行）：

```powershell
cd d:\AppleMusicDesktopLyrics
Remove-Item -LiteralPath publish\current -Recurse -Force
Move-Item -LiteralPath publish\_clean-staging -Destination publish\current
```

---

## 第三部分：遗留状态与注意事项

- 省电模式代码变更完成时处于工作区未提交状态；此后用户继续开发：`Directory.Build.props` 版本已调整为 **3.1.29**，`OverlayPlacementSettings` 新增 `IslandEnabled` 设置（默认开启），设置窗口（`PlacementSettingsWindow.xaml/.cs`）也有相应修改
- `publish\_clean-staging` 仍在，等待替换操作完成
- **版本提示**：该干净产物构建于源码版本 3.1.16。若希望 `current` 反映最新代码（3.1.29），需重新执行一次框架依赖发布，而不是直接换用旧 staging
- 今后发布请统一使用 `.\publish.ps1`（即 `tools\publish-next-version.ps1`），避免手动 `dotnet publish` 漏加 `--self-contained false` 再次造成目录膨胀

## 第四部分：经验教训

1. **版本基线**：功能实现前必须确认主线最新版本（本例中用户已合并新分支，旧基线的实现会被覆盖）；分支合并回主线可能丢失枚举定义导致编译失败，需从其他提交（如 stash）恢复
2. **架构适配**：主线已有真实实现（LyricDockController）时，桥接口/占位枚举等临时设计应果断放弃，改用与现有机制并存的简化方案
3. 手动 `dotnet publish -r <rid>` 不带 `--self-contained false` 默认产出**自包含**发布，会把整个运行时拷入输出目录，是本次目录膨胀的直接原因
4. 替换正在运行的程序目录前，必须先停掉从该目录启动的进程（官方脚本内置此逻辑）
5. 沙箱环境会拦截 `Stop-Process` 与递归删除等特权操作，此类操作需交由用户在本地终端执行
