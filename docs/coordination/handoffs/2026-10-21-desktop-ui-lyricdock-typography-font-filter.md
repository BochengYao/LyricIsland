# 歌词坞字体与中文字体筛选交接

- 日期：2026-10-21
- 任务线程：桌面端岛屿、设置与交互
- 工作目录：`D:\AppleMusicDesktopLyrics`
- 基线提交：`b287506c34c60b714914ace420aeed1b5a50336d`（`main`；工作树开始时落后 `origin/main` 12 个提交）
- 结果提交：未提交、未推送；根工作树含多个既有未提交 WIP，本任务未清理或回滚其他改动
- 允许范围：`LyricHover.App/` 设置、歌词坞渲染及 `LyricHover.Tests/` 对应回归

## 结果

- 歌词坞设置卡新增独立的中文字体、英文字体和歌词字号选项。
- 歌词坞默认保持原视觉：中文 `Microsoft YaHei UI`、英文 `Segoe UI`、主行 `13 DIP`；副行继续使用原有 `12/13` 比例。
- 主行逐字跟随和副行普通文本都按中英文脚本应用对应字体；设置自动应用后立即更新现有歌词坞，不需要重启。
- 顶部歌词岛和歌词坞的中文字体下拉框只枚举实际包含“中、文、歌、曲”代表字形的系统字体。旧配置中已选择但当前检测不到的字体仍会保留为可见选项，避免升级后选择丢失。
- 中文字体下拉框优先读取字体文件内的 `zh-CN` / `zh-TW` 本地化家族名，并为微软雅黑、宋体、黑体、楷体、仿宋、等线等 Windows 常见字体提供中文显示名回退；持久化仍保存稳定的字体规范名，避免跨语言系统失配。
- 修复字体选择后折叠态仍显示打开设置时旧字体名的问题：四个字体框的折叠态直接绑定当前 `SelectedItem.DisplayName` 和 `SelectedItem.SourceName`，显示名与字体样式会随本次选择同步刷新。
- 设置 Schema 从 8 升到 9。旧文件缺失新字段时使用上述默认值；字体名会去除首尾空白，字号规范化到 `8–18 DIP`。

## 修改文件

- `LyricHover.App/ChineseFontFamilyCatalog.cs`
- `LyricHover.App/OverlayPlacementSettings.cs`
- `LyricHover.App/PlacementSettingsWindow.xaml`
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`
- `LyricHover.App/LyricDock/LyricDockController.cs`
- `LyricHover.App/LyricDock/LyricDockWindow.cs`
- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.Tests/Program.cs`
- `Directory.Build.props`（首次候选为 `3.3.15-beta`；中文字体名修正后的最终候选为 `3.3.16-beta`）
- `publish/current/`（本地候选，Git 忽略目录）
- 本交接文件

上述已有文件中的若干项在本任务开始前已有其他未提交修改；后续提交必须按文件和差异块审查，不能整体暂存混合 WIP。

## 自动验证

- `dotnet build LyricHover.sln -c Release --no-restore`：成功，0 错误、310 个既有测试重复链接类型警告。
- `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 下执行 `dotnet run --project LyricHover.Tests -c Release --no-build`：`286 PASS / 0 FAIL`。
- 新覆盖包括 Schema 9 默认值/规范化/克隆、CJK 字形筛选、设置控件存在以及设置到歌词坞渲染链路静态约束。
- `tools/publish-next-version.ps1 -NoLaunch`：成功，脚本内再次完成 `286 PASS / 0 FAIL`、win-x64 Release 构建和发布，最终生成 `v3.3.22 beta`。
- `publish/current/`：11 个文件、33,889,340 bytes；候选未自动启动，没有 `staging-v3.3.22-beta` 残留；上一版 `v3.3.21-beta` 已归档到 `publish/archive/v3.3.21-beta/`。
- `publish/current/LyricHover.App.dll`：FileVersion `3.3.22.0`、ProductVersion `3.3.22-beta`、SHA-256 `7FD8C3336DE1542ED5A8CD549FE93137A2D27F22FDA48D61EDDA25237177D4D2`。
- `publish/current/LyricHover.Core.dll`：FileVersion `3.3.22.0`、ProductVersion `3.3.22-beta`、SHA-256 `248946536A515D6376C5EB35906208895BA36F4D94A6A795DD8DF43B68CD96B8`。
- App/Core 候选哈希分别与本轮 win-x64 Release 输出一致，证明 `publish/current` 已由本轮二进制替换。
- `git diff --check`：通过；仅有工作区既有 LF/CRLF 转换提示。

## 未修改 / 非目标

- 未修改 Core 歌词解析、时间线、播放器或缓存语义。
- 未改歌词坞任务栏定位、Widgets 租约、开关默认值或 Windows 11 支持边界。
- 未生成 MSIX；`publish/current` 仅是框架依赖本地候选，不代表 Store 包或正式发布。
- 未提交 Git、未推送 GitHub、未上传或提交 Microsoft Store。

## 实机验收

自动测试不能替代真实 WPF 字体渲染。合入或发布前至少验证：

1. 顶部歌词岛与歌词坞的中文字体下拉框不再显示 Arial 等无内置中文字形的字体，且 Microsoft YaHei UI 等中文字体可选。
2. 分别切换中英文字体后，混合中英文主行、中文翻译副行和逐字跟随高亮都即时使用正确字体。
3. 字号从 8 调到 18 时，单行/多行、100–200% DPI、主副任务栏上均不裁切、不越过任务栏安全区域。
4. 关闭设置、重启应用后字体和字号保持；Schema 8 用户首次升级时视觉仍为原 13/12 大小。
5. 长歌词触发横向滚动时，字体切换后的测量、居中和左对齐均正确。

## 回滚

回滚时应同时移除 Schema 9 的三个歌词坞字体字段、设置控件、运行时 `ApplyTypography` 链路及中文字体筛选，恢复两个中文字体下拉框的全字体列表；不要只移除设置 UI 而留下持久化字段或渲染接口。
