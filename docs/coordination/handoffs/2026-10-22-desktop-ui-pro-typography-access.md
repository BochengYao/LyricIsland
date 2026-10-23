# 歌词字体、字号与上方角型 Pro 权限交接

- 日期：2026-10-22
- 任务线程：桌面端岛屿、设置与交互
- 工作目录：`D:\AppleMusicDesktopLyrics`
- 基线提交：`c0e083c1bbeac48e5b95efa7d772f6684529ba8f`（`main`）
- 结果提交：未提交、未推送；工作树中存在本任务开始前的并行 WIP，本任务未清理或回滚；本地 `publish/current` 已更新
- 允许范围：`LyricHover.App/` 设置 UI、现有 Pro 权益消费与 `LyricHover.Tests/` 对应回归

## 结果

- 歌词岛与歌词坞的中文字体、英文字体、字号滑块、数值输入框，以及歌词岛“上方角型”分段控件现在都由现有 Pro 权益状态控制。
- `StorePro` 与向旧版付费用户兼容的 `LegacyPro` 均可编辑这些控件；`None` 状态下控件禁用并以 `0.48` 不透明度显示。
- 普通用户悬停禁用控件时显示“此功能仅面向 Pro 支持计划用户开放。”；使用 WPF 禁用控件 Tooltip 支持，鼠标移出后按原生 Tooltip 生命周期自动隐藏。
- 设置窗口打开后主动刷新 Store/缓存权益；购买或重新验证导致权益变化时，控件状态即时同步，无需重开设置窗口。
- Store 成功确认、有效权益缓存确认或测试开关明确切换为无 Pro 后，旧测试版可能留下的自定义字体、字号和凸角状态会恢复并持久化为产品默认值：中文 `Microsoft YaHei UI`、英文 `Segoe UI`、歌词岛字号比例 `1`（基准字号 `17.1`）、歌词坞字号 `13`、上方角型为凹角。纯网络/Store 验证失败不会触发重置。
- 凸角模式的“屏幕边缘距离”继续只影响真实歌词岛与屏幕顶部的距离；设置页预览固定在预览框顶部，不再随距离数值上下移动。
- 真实歌词岛在凸角模式下取消凹角轮廓专用的 `-2.5 DIP` 内容位移，并新增仅在凸角模式显示的“边缘距离”：范围 `0–48 DIP`、默认每侧 `9 DIP`，同一个数值同时进入岛体宽度和高度计算，因此左右与上下安全距离保持一致；凹角仍保持原有 `144 DIP` 总横向留白，以避开内凹轮廓。
- 极端大字号和宽笔画字体不再只按排版推进宽度判断溢出：歌词视口在左右各加入一个渐变宽度的字形墨迹保护区，渐变带随主歌词字号按 `0.7×` 自适应，限制在 `10–36 DIP`，从而在字形接近或越过显示边缘时先渐隐而不是被父级硬裁切。
- 主歌词字号变化现在显式使 `WordTrackingTextBlock` 两层文本与自身测量失效，并在 `DispatcherPriority.Loaded` 阶段重新测量视口、居中位置和跑马灯；从超大字号切回正常字号时不再复用旧的溢出宽度而停在左侧。
- 隐藏测试入口：在“关于”页连续点击 Logo 10 次，再打开“支持开发者”，直接按键输入 `dachengzi` 并按 Enter，可在有 Pro / 无 Pro 间切换。界面不显示测试输入框或输入内容，Logo 保持普通箭头光标；覆盖状态仅在当前设置窗口存活，不写入用户配置，关闭设置窗口后立即清除，下次打开设置时重新使用真实 Store/缓存权益。
- 提示文案已覆盖简中、繁中、英文和日文。

## 修改文件

- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.App/IslandAppearanceRendering.cs`
- `LyricHover.App/OverlayPlacementSettings.cs`
- `LyricHover.App/PlacementSettingsWindow.xaml`
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`
- `LyricHover.App/UiLanguageService.cs`
- `LyricHover.Tests/Program.cs`（含本任务断言；文件内另有任务开始前的岛屿文本渲染 WIP）
- 本交接文件

## 自动验证

- `$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'; $env:TargetPlatformDisplayName='Windows'; dotnet build LyricHover.sln -c Release --no-restore`
  - 成功，0 错误；310 个既有测试重复链接类型警告。
- 同一 Release 构建下，设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 后直接执行 Release 测试程序集
  - `289 PASS / 0 FAIL`，退出码 0；包含 Pro 控件锁定、免费状态恢复字体/字号/凹角默认值、预览固定、凸角四边距离归一化与持久化、极端字号渐变保护、`48 → 15 DIP` 测量刷新与重新居中、隐藏测试码切换及关闭设置清除覆盖的回归。
- `tools/publish-next-version.ps1 -NoLaunch`
  - 成功，版本源由 `3.3.24-beta` 递增到 `3.3.25-beta`；脚本内再次完成桌面回归、win-x64 Release 构建和框架依赖发布，候选未自动启动。
  - 用户反馈后使用 `-KeepVersion -NoLaunch` 同版本重建最终候选；`publish/current/`：11 个文件、33,900,480 bytes；`FileVersion 3.3.25.0`、`ProductVersion 3.3.25-beta`。
  - `LyricHover.App.dll` SHA-256：`860BBA83C865D3D359A3FB1A498C7E74D18E3F698CBDA9EEA59782B1366F19F6`。
  - `LyricHover.Core.dll` SHA-256：`F16A275887D3D1554B9BF0CD20417E77F4E2F218377A89CFA1DDAF3DE89F88FD`。
  - App/Core 哈希分别与同次 win-x64 Release 输出一致；本轮替换前候选归档到 `publish/archive/v3.3.25-beta-20261023-003844/`，此前同版本候选依次在 `publish/archive/v3.3.25-beta-20261023-001257/`、`publish/archive/v3.3.25-beta-20261022-235821/`、`publish/archive/v3.3.25-beta-20261022-235242/`、`publish/archive/v3.3.25-beta-20261022-232132/` 与 `publish/archive/v3.3.25-beta/`。没有本版本 staging 残留；仓库保留一个 2026-08-24 的历史诊断目录 `publish/staging-diagnose-v3.1.39-Beta/`，本轮未修改。
- `git diff --check`
  - 通过；仅报告工作区既有 LF/CRLF 转换提示。

## 未修改 / 非目标

- 未改变 Pro 商品 ID、Store 查询、旧版付费用户截止规则或权益缓存语义。
- 设置 Schema 从 9 升至 10，新增 `IslandConvexContentEdgeDistance` 持久化字段；旧配置自动使用 `9 DIP` 默认值，异常值归一化到 `0–48 DIP`。字体/字号/角型原有字段与 Pro 商品、权益语义均未改变。
- 已打包到本地 `publish/current`；未生成 MSIX，未提交 Git、推送 GitHub 或发布 Microsoft Store。

## 实机验收

自动测试不能代替真实 WPF/Store 交互，发布前仍需验证：

1. 未拥有 Pro 的 Microsoft 账号打开“歌词外观”时，歌词岛和歌词坞的字体/字号控件及“上方角型”控件均变灰，无法通过鼠标或键盘修改；旧凸角设置应自动恢复并持久化为凹角。
2. 鼠标悬停每个禁用控件时出现指定提示，移出后提示立即消失。
3. `StorePro` 与 `LegacyPro` 两类账号验证完成后控件即时恢复正常不透明度并可编辑。
4. 断网但存在上次成功 Pro 缓存时仍可解锁；无有效缓存且 Store 验证失败时保持锁定。
5. 简中、繁中、英文、日文界面中的提示文案正确，切换语言后无需重开窗口。
6. “关于”页 Logo 使用普通箭头光标；连续点击 Logo 10 次后打开“支持开发者”，页面不新增测试输入框，直接按键输入 `dachengzi` 并回车即可来回切换。确认输入字符始终不可见、切到无 Pro 时立即恢复默认字体/字号与凹角；关闭设置窗口后测试覆盖消失，再次打开设置时显示真实 Store/缓存权益。
7. 凸角模式拖动“屏幕边缘距离”时，真实歌词岛仍改变与屏幕顶部的距离，但设置页预览不发生上下移动。
8. 在凸角模式拖动“边缘距离”，确认真实歌词岛四周留白同步增减、数值重启后保留；重点检查 `0 DIP`、默认 `9 DIP` 与上限 `48 DIP`，内容在常用值下不进入圆角裁切区。再切回凹角，确认该设置项隐藏且原留白与垂直补偿不变。
9. 将主歌词字号调至 `48 DIP` 并使用宽笔画中英混排长句，确认接近/越过歌词视口的左右边缘呈连续渐隐而非硬切；随后切回 `15–17.1 DIP`，确认同一主行立即恢复水平居中，跑马灯和逐字高亮位置同步重算。
