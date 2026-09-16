# 任务交接：歌词坞可读性、跟随动画与逐字追踪性能稳定性

- 日期：2026-09-16
- Owner：Desktop Island, Settings & Interaction UI
- 工作分支：`codex/feature/desktop-fullscreen-auto-hide`
- 依赖功能提交：`9f9d4e87b0c51395d296f5c4cd1f00ba9389a3b2`（歌词坞跟随任务栏隐藏与恢复）
- 本轮功能提交：`059d88b`、`6cf5a74`、`cc2c31f`、`eb9e859`
- 当前功能 HEAD：`eb9e8592263bbe2a1b2c2d824edb0e277104b5cb`
- 交接对象：Desktop UI & Interaction；Quality & Release；Architecture；原歌词坞全屏隐藏修复任务
- 状态：实现、自动化验证、`publish/current` 本地打包完成；分支推送、主线集成和真实 Windows 视觉验收待完成
- 允许范围：歌词岛/歌词坞 WPF 呈现、歌词坞位置探测、相关回归测试、版本源与本交接

## Summary

本轮完成了歌词坞在透明任务栏和浅色背景上的可读性增强、Win11 左对齐任务栏图标变化时的非线性跟随动画，以及歌词岛/歌词坞逐字追踪和歌词翻页的性能优化。随后修复了句子切换完成时首词进度短暂回退造成的闪烁回弹。

歌词坞现在使用反色细描边和零偏移柔和阴影；中文次行单独提高了对比度。位置变化采用 260 ms `QuarticEaseOut` 动画。歌词映射结果在层之间共享复用，离场层在换句过程中保持冻结，入场层在动画结束时把当前实时投影交给稳定层，避免首词从旧采样位置重新开始。

任务栏位置/UI Automation 探测已移出 WPF UI 线程并串行合并；歌词快照变化不再触发冗余 Shell 探测。全屏或任务栏自动隐藏时歌词坞继续临时隐藏，并在任务栏恢复后重新显示，且不改变用户的启用设置。

## Decisions

1. Core 继续拥有歌词时间线真值；UI 只缓存和投影既有词级时间，不建立第二套播放时钟。
2. 换句动画结束时，以入场层的实时投影作为稳定层起点；不回用 280 ms 之前捕获的旧进度。
3. 旧歌词离场层冻结其投影，避免两个 WPF 图层同时持续重算同一组字形映射。
4. Shell/UIA 探测在后台线程执行，并通过串行、合并与结果复用限制并发；UI 线程只消费最终位置结果。
5. `publish/current` 仅作为本地可回滚候选，不代表 GitHub、Store 或 ESA 已发布。

## Root Causes

- 逐字追踪与翻页卡顿：歌词岛和歌词坞的多个图层在每次呈现时重复规范化词时间、建立字形映射，并且歌词刷新会同步触发较重的 Shell/UIA 位置探测。
- 首词闪烁回弹：280 ms 换句动画完成后，稳定层接手的是较早采样的词进度；下一帧再跳到实时进度，视觉上表现为首词先回退再追上。
- 中文次行可读性不足：英文主行的描边/阴影参数直接用于更细、更小的中文次行时，对浅色透明背景的边缘分离不够。
- 图标数量变化时跳动：歌词坞此前直接应用新锚点，没有为任务栏左对齐布局的横向变化提供缓动过渡。

## Changed Areas

- `LyricHover.App/LyricDock/LyricDockWindow.cs`：位置缓动、反色细描边、零偏移柔和阴影、中文次行对比度、换句和尺寸取消路径的实时投影交接。
- `LyricHover.App/LyricDock/LyricDockController.cs`：歌词快照与位置探测解耦，跳过未变化的布局/主题工作，缓存并共享词映射。
- `LyricHover.App/LyricDock/WindowsLyricDockEnvironment.cs`：Shell/UIA 探测后台化、串行合并及跨线程安全。
- `LyricHover.App/LyricDock/ILyricDockEnvironment.cs`：位置环境契约补充。
- `LyricHover.App/Modules/WordTrackingTextBlock.cs`：词投影锚点、500 ms 内时间线抖动容忍、映射缓存和转场接力。
- `LyricHover.App/Modules/LyricsModuleView.xaml.cs`：歌词岛换句完成时接收实时投影。
- `LyricHover.Tests/Program.cs`：位置动画、对比度、缓存复用、探测解耦、时间线抖动和首词回退回归覆盖。
- `Directory.Build.props`：候选版本推进至 `3.2.47-Beta`。

## Verification

- 桌面完整回归：267 PASS、0 FAIL。
- win-x64 Release build/publish：0 warning、0 error。
- `git diff --check`：通过；仅对当前无关网站 WIP 输出既有 LF/CRLF 转换提示。
- 80 个词时间单元、歌词岛/歌词坞四层映射的本地微基准：平均约 1.35 ms，p95 约 2.35 ms。
- 修复前现场诊断中，旧候选 UI 线程约占单核 20.94%，进程峰值约占单核 76.11%；早期任务栏探测 p95 15.66 ms、最大 34.94 ms。该数据用于定位阻塞来源，不是跨机器性能承诺。
- 自动化与微基准不能替代真实 WPF 动画、任务栏和多显示器视觉验收。

## Local Candidate

- 路径：`D:\AppleMusicDesktopLyrics\publish\current`
- ProductVersion：`3.2.47-Beta`
- FileVersion：`3.2.47.0`
- 文件：8 个，共 24,573,395 bytes
- `LyricHover.App.exe` SHA-256：`D7B18CA55E019ECE94E71A497D0F64EDDDFA9D14721036716A2C1507D31A6279`
- `LyricHover.App.dll` SHA-256：`EA9488E2045C27BC5DCD7FDF9211329BAC43EB24C5106CFAAE39E5AF11F9B6AE`
- `LyricHover.Core.dll` SHA-256：`F837364E6C9D3BC2DDCF6BD6D45F3766887509C66AD91787CDC6630152B2F1D4`
- 打包方式：`publish.ps1 -KeepVersion -NoLaunch`；候选未自动启动。
- 被替换候选归档：`publish/archive/v3.2.46-Beta-20260916-234521`。

## Git / Release State

- 当前分支相对 `origin/codex/feature/desktop-fullscreen-auto-hide` ahead 4；本轮四个功能提交尚未推送。
- 本轮交接提交应只包含本文件。网站、宣传图与 `output/canva-assets` 等其他线程 WIP 必须继续保持未暂存。
- 未创建 GitHub Release、标签或附件；未上传、验证或提交 Microsoft Store、ESA。
- 主线集成前必须重新确认目标提交与 `publish/current` 哈希；若合并后重建，候选证据需要随新提交更新。

## Risks / Open Questions

1. 全屏监视器正常模式仍约每 300 ms 请求一次位置刷新；后台化和合并已解除 UI 阻塞，但是否把完整 Shell/UIA 探测进一步降到约 1 s 或改为失效驱动，需要 Architecture 基于同环境测量确认。
2. 首词回弹、整首 QRC/YRC 翻页流畅度和鼠标移开后的悬停响应仍需真实 WPF 目测，自动化不能证明视觉连续性。
3. 反色描边和阴影需要覆盖透明任务栏、浅色/深色壁纸、HDR/缩放组合；中文次行尤其需要验收。
4. 任务栏位置探测涉及 Explorer、Widgets、DPI 和多显示器边界；Explorer 重启与 Widgets 租约恢复尚无本轮真机证据。
5. 测试工程链接应用源码时存在既有 `CS0436` 类型冲突警告；正式 App build/publish 为 0 warning、0 error，但测试项目清理可另立任务，不能混入本交接。

## Action Items

| Priority | Owner | Action | Due |
| --- | --- | --- | --- |
| P0 | Desktop UI & Interaction | 在真实 WPF 中验证每句首词无回弹、整首逐字追踪和翻页连续性，覆盖歌词岛与歌词坞 | 合并前 |
| P0 | Desktop UI & Interaction | 验证透明任务栏与浅/深色背景下主行、中文次行的描边和阴影可读性 | 合并前 |
| P0 | Quality & Release | 按当前 HEAD、版本和三项 SHA-256 复核 `publish/current`；集成后若重建则更新候选证据 | 发布前 |
| P1 | Architecture | 复核 300 ms 监视节拍与完整 Shell/UIA 探测成本，决定是否需要约 1 s 节流或 ADR | 合并前 |
| P1 | Desktop UI & Interaction | 验证 Win11 左对齐任务栏增减图标时 260 ms 非线性跟随，并覆盖自动隐藏、全屏、暂停/等待播放 | 合并前 |
| P1 | Quality & Release | 覆盖多显示器、DPI、省电模式、Explorer 重启与 Widgets 租约；记录真实 Windows 结果 | 发布前 |
| P1 | Integrator | 决定并执行分支推送/主线集成；不得把当前网站和宣传素材 WIP 带入提交 | 外部发布前 |

## Scope Boundary

- 未修改歌词来源、Core 时间线、缓存格式、设置键、默认值、Store 标识、单实例名称或数据目录迁移。
- 临时隐藏不持久化为关闭，也不重建 Widgets 租约。
- 本交接不宣称完成真实 Windows 视觉验收、主线集成或外部发布。
