# 任务交接：设置窗口外圆角与无描边对齐

- 日期：2026-08-23
- 任务线程：Desktop UI & Interaction / `codex/feature/desktop-settings-outer-chrome`
- 基线提交：`eb94050`
- 写入范围：设置界面圆角资源、设置窗口 DWM 属性、对应静态回归测试与本交接

## 问题

- `RootChrome` 已经是 `BorderThickness="0"`，截图中的最外圈细线实际来自 Windows 11 DWM 非客户区边框。
- 顶层窗口由 `DWMWCP_ROUND` 使用 Windows 11 固定 8px 圆角，而大卡片仍使用 18px，因此外层和内部大卡片转弯半径不一致。

## 修复

- `RadiusLarge` 由 18px 调整为 8px，使顶层窗口、大卡片、侧栏卡片和关联弹窗使用同一大轮廓半径。
- 给设置窗口设置 `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE`，显式关闭 DWM 外边框；XAML 根容器继续保持零描边。
- Toggle、滑块圆点和预览岛等真正胶囊仍使用与控件高度匹配的明确半径，未回退到 `999`。

## 验证

- `dotnet build LyricHover.sln -c Release`：退出码 0；环境首次因沙箱无法读取用户 Windows SDK 路径失败，使用既定 SDK 环境变量后通过。
- `dotnet run --project LyricHover.Tests -c Release --no-build`：退出码 0，完整测试全部 PASS。
- `git diff --check`：通过。
- 150% DPI 真实窗口截图：`C:\Users\14731\AppData\Local\Temp\settings-outer-chrome-after-150.png`（1560×1080）。截图可见外圈灰色细描边已消失，大卡片与外层使用相同 8px 转弯半径。

## 状态边界

- 已完成代码与自动化验证：是。
- 已完成 150% DPI 真实窗口截图：是。
- 已更新 `publish/current`：否，由后续 3.1.33 发布步骤负责。
- 已上传 GitHub / Microsoft Store：否。
