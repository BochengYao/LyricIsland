# 任务交接：设置模块四边安全距离统一

- 日期：2026-08-23
- 任务线程：Desktop UI & Interaction / `codex/feature/settings-equal-card-insets`
- 基线提交：`71daa77`
- 写入范围：设置窗口 XAML、对应静态回归测试与本交接

## 问题

- 通用等设置模块使用左右 32px、上下 20px 的非对称内边距。
- 歌词岛与歌词坞模块又局部覆盖为四边 40px，导致不同页面的内容起止线不一致。

## 修复

- `SettingsGlassCardStyle` 统一使用四边 24px 内边距，遵循 Apple 工具卡片的 8px 结构节奏。
- 移除歌词岛、歌词坞模块的 40px 局部覆盖，所有设置模块只从同一个全局样式获取安全距离。
- 回归测试明确要求全局 24px，并禁止重新引入 `32,20` 或 40px 模块覆盖。

## 验证

- `dotnet restore LyricHover.sln`：退出码 0。
- `dotnet build LyricHover.sln -c Release --no-restore`：退出码 0，0 错误；测试工程有 157 条既有 `CS0436` 源文件/导入类型冲突警告。
- 沙箱内首次完整测试仅 `release version mutation is transactional and serialized` 因虚拟用户 NuGet 缓存不可读失败，其余测试通过；这是环境问题，不是产品回归。
- 沙箱外 `dotnet run --project LyricHover.Tests -c Release --no-build`：退出码 0，完整测试全部 PASS。
- 尝试使用一次性 WPF 工具生成新截图，但工具入口程序集无法正确承载产品图片资源；按重复失败停止规则删除了整个临时工具，未将其纳入提交。此次没有新的实图证据。

## 手动验收条件

- 打开通用、歌词外观、位置与状态、鼠标避让、快捷键、模块布局、支持和关于页面。
- 每张模块卡片的内容边界距卡片上、右、下、左边缘均为 24px；歌词岛与歌词坞不得再出现 40px 特例。

## 状态边界

- 已完成代码与自动化验证：是。
- 已完成新实图验证：否，受临时预览工具资源装载限制。
- 已更新 `publish/current`：否，由后续 v3.1.35 发布步骤负责。
- 已上传 GitHub / Microsoft Store：否。
