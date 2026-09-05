# 任务交接：Quality & Release / 节能模式 3.1.73-Beta 本地候选

- 日期：2026-08-25
- 来源线程：节能模式性能优化与详情文案修订
- 候选提交：`815444f9363d3567772296924731d472c205f649`
- 功能父提交：`47d309ede151bda0c5655ea510337b1f9a60c172`
- 本地候选：`publish/current`，`3.1.73-Beta`，framework-dependent `win-x64`。
- DLL SHA-256：`40EF97FE89888BAD12347BA09C0CA6C46751C7571C6DE67BAF32208A82DB42D4`。

## Task Contract

- Goal：交接节能模式功能及其详情文案已进入 `publish/current` 的可追溯本地候选状态。
- Owner：Quality & Release。
- Allowed Write Scope：候选核验、实机验收、发布门禁与发布记录；仅在用户另行授权时上传或发布。
- Forbidden Scope：不得把本地 `publish/current` 称作 GitHub、Store、官网或其他外部渠道发布；不得清理现有其他线程 WIP。
- Handoff Status：**Knowledge Sync / 无外部发布待办**。

## 候选内容

- 版本源：`Directory.Build.props` 已从 `3.1.72-Beta` 递增至 `3.1.73-Beta`。
- 功能包括设置窗口 Draft 即时视觉预览、实体背景/低阴影/低动画策略、播放 1 秒刷新和无展示需求时 4 秒空闲刷新。
- 后续修订仅更新节能模式详情文案、本地化与回归断言，仍属于同一 `3.1.73-Beta` 本地候选；没有再次递增版本。

## 已执行验证

| 命令 / 检查 | 结果 |
| --- | --- |
| `dotnet build LyricHover.sln -c Release` | 成功，0 error；测试项目 189 条既有 `CS0436` 重复类型警告。 |
| `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1 dotnet run --project LyricHover.Tests -c Release --no-build` | 通过。跳过的是会启动独立发布事务的环境夹具，不是产品回归。 |
| `dotnet restore LyricHover.App\LyricHover.App.csproj --runtime win-x64` | 成功；为发布目标写入 `win-x64` assets。 |
| `publish.ps1 -KeepVersion -NoLaunch` | 成功，输出“发布完成：v3.1.73 Beta”。 |
| `publish/current` | 8 个文件，存在 `LyricHover.App.deps.json` 与 `LyricHover.App.runtimeconfig.json`。 |
| 程序集与哈希 | `LyricHover.App.dll` AssemblyVersion 为 `3.1.73.0`；哈希与 `LyricHover.App/bin/Release/netcoreapp3.1/win-x64` 输出一致。 |

## 已知环境事项

- 常规 solution build 会把 `project.assets.json` 恢复为默认目标；紧接着执行发布脚本可能报 `NETSDK1047`（缺少 `netcoreapp3.1/win-x64` target）。发布前先执行上表的 `--runtime win-x64` restore 即可。
- 沙箱环境直接读取本机 SDK 路径会报访问限制；使用本机 Windows SDK 环境变量完成的构建和发布已通过。这不代表产品构建失败。
- 本地包未启动（`-NoLaunch`），也未执行 GitHub、Store、官网上传、审核或发布。

## 建议的后续验收

1. 在真实 Windows 环境按普通/节能、浅色/深色/高对比度和 100%/150% DPI 验收设置窗口即时预览、Cancel 恢复和 Apply 后保持。
2. 验收播放刷新与无会话空闲刷新；媒体会话变更必须保持即时响应。
3. 只有在获得用户明确授权且有干净、可审计基线时，才将此候选推进至外部渠道；当前结论仅为 **已构建、已验证、已打包到本地 `publish/current`**。
