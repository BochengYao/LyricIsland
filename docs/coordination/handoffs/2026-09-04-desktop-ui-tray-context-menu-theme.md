# 任务交接：Desktop UI / 托盘右键菜单主题化

- 日期：2026-09-04
- 任务线程：Desktop Island, Settings & Interaction UI
- 基线提交：`1de81933050c830d228a93069424ba038bb3fe5d`
- 结果提交：`305dbd82217a6d36f8923e5e1f860bc3f3e9cf4d`
- 分支：`codex/feature/desktop-tray-menu-theme`
- 允许修改范围：`LyricHover.App/` 的托盘 UI 与直接回归测试
- Handoff Status：Feature Handoff / 本地候选已生成，待 UI 实机视觉确认

## 已完成

- 将默认 Windows `ContextMenuStrip` 渲染替换为 LyricHover 自定义托盘菜单渲染层。
- 使用 12px 圆角、轻边框、系统下拉阴影、紧凑间距、分隔线、悬停与按压反馈。
- 为“偏好设置”和“退出”绘制主题感知的齿轮、电源线性图标，不新增外部图片资产。
- 浅色使用白色卡片与深色文字；深色复用设置页 `#2C2C2E` / `#F5F5F7` 色阶。
- Apply 设置主题后立即刷新托盘菜单；跟随系统模式在每次菜单打开前重新读取 `AppsUseLightTheme`。
- 高对比度模式回退到 Windows `SystemColors`，避免自定义色破坏可访问性。
- 保留托盘双击打开设置、菜单打开设置和退出应用的原行为。

## 未修改 / 非目标

- 未修改设置模型、持久化键、Core、播放器、歌词、缓存或时间线。
- 未上传 GitHub、未提交 Microsoft Store、未发布官网或任何外部渠道。

## 本地候选

- 版本提交：`d7736e94c1a9ec1daa72a4571f7fbb5c92503233`。
- 当前版本：`3.2.35-Beta`，framework-dependent `win-x64`。
- 目录：`publish/current`，11 个文件，共 33,788,242 bytes。
- `LyricHover.App.dll` SHA-256：`5AA22E4C64D98A19DBCEC26E8978148F51AFEEC149C1B6E3DE85032BB7273C6D`。
- `LyricHover.App.exe` SHA-256：`1FF4A9D1DE3FC104F52BFD3A5C78237CBDAF02B71E679D994C37691BEEB173F7`。
- 旧 `3.2.34-Beta` current 已由权威脚本归档为 `publish/archive/v3.2.34-Beta-20260904-191531`。

## 验证

- 命令：设置 `TargetPlatformSdkPath` / `TargetPlatformDisplayName` 后运行 `dotnet build LyricHover.sln -c Release --no-restore`。
- 结果：成功，0 error；测试项目保留 190 条既有 `CS0436` / 未使用事件警告。
- 命令：设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 后运行 `dotnet run --project LyricHover.Tests -c Release --no-build`。
- 结果：全部执行项 PASS；新增源码契约测试和运行时菜单实例测试，确认浅/深配色、渲染器、菜单结构和主题刷新入口。
- 命令：设置 Windows SDK 路径后运行 `publish.ps1 -NoLaunch`。
- 结果：完整回归 PASS；`win-x64` Release 构建 0 warning、0 error；发布脚本输出 `发布完成：v3.2.35 Beta`。
- 命令：`dotnet run --no-restore --configuration Release --project LyricHover.Tests -- --release-version-fixture`。
- 结果：在可读取真实用户 NuGet 缓存的环境中 PASS；发布版本变更保持事务化与串行化。
- 产物一致性：`publish/current/LyricHover.App.dll` 与同次 Release `win-x64` 输出 SHA-256 完全一致；deps/runtimeconfig 均非空，目标 staging 已清理。
- Computer Use：尝试启动当前构建时，被正在运行的 `publish/current` 单实例正确拦截；托盘驻留窗口不向 Computer Use 暴露可绑定窗口，因此未冒险关闭用户当前实例或误操作其他窗口。

## 风险与后续

- 已知限制：缺少真实托盘弹出后的浅色、深色和 150% DPI 截图；自动化与运行时对象测试不能替代最终观感确认。
- 交接目标：Desktop UI / User Acceptance；从 `publish/current/LyricHover.App.exe` 启动，分别在浅色、深色、跟随系统和高对比度下右键托盘图标。
- 回滚点：回滚 `305dbd82217a6d36f8923e5e1f860bc3f3e9cf4d` 即恢复默认 `ContextMenuStrip`。
