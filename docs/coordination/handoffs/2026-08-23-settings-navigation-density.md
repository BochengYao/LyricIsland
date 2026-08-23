# 任务交接：设置导航密度与滚动条优化

- 日期：2026-08-23
- 任务线程：Desktop UI & Interaction / `codex/feature/settings-navigation-density`
- 基线提交：`1ac2a4e`
- 写入范围：设置窗口 XAML、对应静态回归测试与本交接

## 问题

- 设置页滚动条是一根长期可见的粗灰条，视觉重量过高，不符合轻量浮层式滚动提示。
- 通用页卡片上下留白及卡片间距偏大，常用内容无法在 150% DPI 的窗口内完整呈现。
- 左侧导航行距偏密、文字对比度偏低，且功能顺序没有优先体现用户最常进入的歌词外观与布局设置。

## 修复

- 滚动条改为 8px 透明交互轨道内居中的 4px 浮层滑块；空闲淡出、内容悬停淡入，滑块悬停扩展为 6px；无可滚动内容时直接折叠。
- 全局设置卡片纵向内边距由 28px 收紧为 20px，页标题下间距由 24px 收紧为 18px；通用页卡片间距由 26px 收紧为 18px，辅助说明下间距同步收紧。
- 侧栏行高由 32px 调整为 38px，字号由 14px 调整为 14.5px，默认文字/图标不透明度由 0.5 提升至 0.72，并增加图标与文字间距。
- 导航按使用兴趣重排为：通用 → 歌词外观 → 模块布局 → 位置与状态 → 鼠标避让 → 快捷键 → 关于；“支持开发者”继续保持底部独立入口。

## 验证

- `dotnet build LyricHover.sln -c Release`：退出码 0，0 错误；测试工程仍有 157 条既有 `CS0436` 源文件/导入类型冲突警告。
- `dotnet run --project LyricHover.Tests -c Release --no-build`：退出码 0，完整测试全部 PASS，包含新增的导航密度与优先级回归测试。
- `git diff --check`：通过，仅提示现有 Windows 行尾转换。
- 150% DPI 通用页实图：`C:\Users\14731\AppData\Local\Temp\settings-navigation-density-general-after-150.png`（1560×1080），三张卡片无需滚动即可完整呈现。
- 150% DPI 歌词外观页实图：`C:\Users\14731\AppData\Local\Temp\settings-navigation-density-scrollbar-after-150.png`（1560×1080），可见轻量浮层滚动滑块。

## 状态边界

- 已完成代码、自动化验证和 150% DPI 实图检查：是。
- 已更新 `publish/current`：否，由后续 3.1.34 发布步骤负责。
- 已上传 GitHub / Microsoft Store：否。
