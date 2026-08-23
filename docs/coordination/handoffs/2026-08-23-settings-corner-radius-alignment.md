# 任务交接：设置界面圆角矩形与胶囊几何统一

- 日期：2026-08-23
- 任务线程：桌面端岛屿、设置与交互 / `codex/feature/desktop-settings-radius-alignment`
- 基线提交：`f62232c55a5434903891031a5740f60b5b8f55b6`
- 结果提交：本交接记录与实现位于同一提交
- 允许修改范围：`LyricHover.App/` 设置界面与关联弹窗、`LyricHover.Tests/Program.cs`、本交接文件

## 已完成

- 复现 150% DPI 下 `CornerRadius=999` 将左侧选中态和“完成”按钮渲染为透镜/椭圆的问题。
- 新增应用级 `CornerRadiusResources.xaml`，统一圆角矩形为 Large 18 / Medium 14 / Small 10。
- 左侧导航选中态与普通/完成按钮统一使用 Small 10，恢复圆角矩形语义。
- Toggle、Slider 圆形滑块、歌词岛与频谱预览改用各自高度一半的显式胶囊半径，删除 `RadiusPill=999`。
- 设置主窗、信息弹窗、支持者刻印确认窗、任务栏歌词确认窗共同合并同一资源字典。
- 增加静态回归测试，约束共享资源、禁止 999 半径，并清除旧的临时截图测试入口。

## 未修改 / 非目标

- 未修改 `LyricHover.Core/`、设置模型/持久化、播放器、歌词逻辑、版本号、发布脚本或 `publish/current`。
- 布局卡内按比例缩小的示意插画、细轨道和歌词岛产品本体几何仍保留独立半径，不强行套用设置控件半径。

## 验证

- 修复前截图：`%TEMP%\settings-radius-before-150.png`，左侧选中态和“完成”为椭圆。
- 修复后截图：`%TEMP%\settings-radius-after-150.png`，左侧选中态和“完成”为一致的 10px 圆角矩形，Toggle 保持胶囊。
- 命令：`git diff --check`
- 结果：通过。
- 命令：`dotnet build LyricHover.sln -c Release`
- 结果：退出码 0，0 错误；存在项目既有的重复类型编译警告。
- 命令：`dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：退出码 0，完整测试全部 PASS；新增 `settings surfaces share semantic corner radii` PASS。

## 风险与后续

- 已知限制：离屏证据覆盖 150% DPI 深色通用页；未在多台真实设备逐一截图，但共享样式和完整测试已覆盖所有设置页及关联弹窗的资源引用。
- 交接目标：项目统筹 / 桌面发布线程决定是否合并与生成下一版本候选。
- 回滚点：基线提交 `f62232c55a5434903891031a5740f60b5b8f55b6`。
