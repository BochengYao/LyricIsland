# 任务交接：设置页语言标识

- 日期：2026-08-11
- 任务线程：04-desktop-island-settings-ui
- 基线提交：cd93a62780848670a0ead5dbd77acf6fc0366d87
- 结果提交：未提交（共享工作树）
- 允许修改范围：`LyricHover.App/` 设置界面与 `LyricHover.Tests/` 本地化 UI 回归测试。

## 已完成

- 将侧栏语言选择器上方的可见文字“语言”替换为跨语言通用的 `文 / A` 标识。
- 保留 `ToolTip` 与 `AutomationProperties.Name` 为“语言”，由既有多语言服务按当前语言提供可读说明；不会以图标损害读屏或发现性。
- 将 `文 / A` 注册为四种界面语言通用符号，满足全局可见本地化覆盖门禁。

## 未修改 / 非目标

- 未修改语言选择逻辑、默认值、持久化设置、歌词/播放器 Core 语义、Store 身份或发布产物。

## 验证

- 命令：`$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'; $env:TargetPlatformDisplayName='Windows'; dotnet build LyricHover.sln -c Release`
- 结果：退出码 0；0 警告、0 错误。
- 命令：`$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'; $env:TargetPlatformDisplayName='Windows'; dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：退出码 0；完整桌面回归通过，含四语种静态文案覆盖、运行时本地化与两项历史失败基线（本次也通过）。
- 命令：`dotnet build LyricHover.App\LyricHover.App.csproj -c Release -p:OutputPath='C:\Users\14731\AppData\Local\Temp\LyricHover-language-symbol-final\'`
- 结果：退出码 0；独立验收程序：`C:\Users\14731\AppData\Local\Temp\LyricHover-language-symbol-final\LyricHover.App.exe`。
- 手动验收条件：退出托盘中的旧 LyricHover 单实例后，运行该 EXE；确认侧栏显示 `文 / A`，鼠标悬停显示当前语言的“语言”提示，并用读屏检查该控件说明；依次切换四种界面语言，确认语言选项仍按原生语言显示。

## 风险与后续

- 已知限制：当前线程无桌面自动化控制，未执行真实鼠标与读屏操作；旧单实例会接管新 EXE，需先退出。
- 交接目标：如需改用图形图标而非文字符号，交给 04-desktop-island-settings-ui，并提供已确认的 SVG 资产或视觉规范。
- 回滚点：恢复 `PlacementSettingsWindow.xaml` 的 `Text="语言"`，并移除 `UiLanguageService.cs` 中的 `文 / A` 通用映射与本任务断言；基线提交为 `cd93a62780848670a0ead5dbd77acf6fc0366d87`。
