# 任务交接：支持与关于页 Apple 风格整理

- 日期：2026-08-23
- 任务线程：Desktop UI & Interaction / `codex/feature/settings-support-about-apple`
- 基线提交：`3709650`
- 写入范围：设置窗口 XAML、对应静态回归测试与本交接

## 问题

- 支持页使用四列营销卡片，标题、说明和链接被压缩，阅读顺序分散，Pro 区域的字号和留白也偏重。
- 关于页标题、副标题与版本信息字号差距过大，Logo 阴影较重，并长期展示已经过期的 v2.0 更新内容。
- 两页没有形成系统设置常见的“分组行 + 单一强调色 + 稳定字号阶梯”，在 150% DPI 下显得拥挤且不够克制。

## 修复

- 支持页改为 2×2 分组设置行：统一 72px 行高、22px 图标、14.5px 标题和 12.5px 说明，并保留评价、分享、GitHub 与反馈的原有点击行为。
- 支持页收紧 Pro 卡片层级和模块间距，统一卡片四边 20px 内边距，去掉表情和重复营销文案。
- 关于页将 Logo 调整为 48px 并移除阴影，建立 22px 产品名、14.5px 副标题、12.5px 元数据的稳定层级；操作行统一为 60px。
- 关于页用简洁产品说明替换过期的 v2.0 更新清单，保留官网、GitHub、教学模式及版本信息功能。
- 视觉方向采用 Apple 系统设置的分组列表、低对比度分隔线与单一蓝色交互强调；未复制品牌素材。

## 验证

- `dotnet restore LyricHover.sln`：退出码 0。
- `dotnet build LyricHover.sln -c Release --no-restore`：退出码 0，0 错误；测试工程仍有 157 条既有 `CS0436` 警告。
- `dotnet run --project LyricHover.Tests -c Release --no-build`：退出码 0，完整测试全部 PASS，包含支持页和关于页结构回归测试。
- `git diff --check`：通过，仅提示现有 Windows 行尾转换。
- 150% DPI 支持页实图：`C:\Users\14731\AppData\Local\Temp\settings-support-apple-after-150.png`（1560×1080）。
- 150% DPI 关于页实图：`C:\Users\14731\AppData\Local\Temp\settings-about-apple-after-150.png`（1560×1080）。

## 状态边界

- 已完成代码、自动化验证和 150% DPI 实图检查：是。
- 已更新 `publish/current`：否，由后续 3.1.36 发布步骤负责。
- 已上传 GitHub / Microsoft Store：否。
