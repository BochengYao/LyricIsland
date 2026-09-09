# 任务交接：专辑封面缩放质量

- 日期：2026-09-09
- 任务线程：Desktop Island, Settings & Interaction UI（Sol 中度编码与初检）
- 基线提交：`82e921a85182579367bf36ddf25fd2d2b10a9f72`
- 结果提交：本交接与代码同次提交，以 Git 历史记录为准
- 工作分支：`codex/feature/desktop-album-art-quality`
- 允许修改范围：`LyricHover.App/Modules/AlbumArtModuleView.xaml`、`LyricHover.Tests/Program.cs`、本任务独立交接记录

## 已完成

- 在 `ArtworkImage` 上显式声明 `RenderOptions.BitmapScalingMode="HighQuality"`，让 WPF 对缩放后的封面使用高质量重采样。
- 将既有封面布局测试扩展为结构化 XAML 检查，直接约束 `Image` 的高质量缩放属性；测试不依赖属性顺序或换行。
- 保留 42 DIP 模块尺寸、`UniformToFill`、圆角裁切、原始字节整图解码及 `BitmapCacheOption.OnLoad`。

## 未修改 / 非目标

- 不修改 `LyricHover.Core/`、媒体会话、封面原始字节获取、解码尺寸、缓存语义、模块尺寸或裁切。
- 不设置 `DecodePixelWidth` / `DecodePixelHeight`，避免在未知 DPI 下预先丢失源图像素。
- 不修改版本、共享治理文档、发布产物或根工作树 WIP。

## 验证

- 命令：`git diff --check`
- 结果：退出码 0；仅有 Git 的 LF/CRLF 工作区提示，无空白错误。
- 命令：设置 Windows SDK 环境变量后执行 `dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:"ErrorsOnly;Summary"`
- 结果：退出码 0；245 个警告，0 个错误；警告类别为测试工程既有的 `CS0436` 类型冲突及 `CS0067` 未使用事件。
- 命令：`dotnet run --project LyricHover.Tests -c Release --no-build`
- 结果：退出码 0；247 PASS、0 FAIL；新增约束以 `album art uses high quality scaling with a rounded clip` 通过。
- 环境说明：沙箱内首次普通构建因无权读取用户 `NuGet.Config` 退出 1，随后 `--no-restore` 因隔离工作树尚无 `project.assets.json` 退出 1；经授权读取本机 NuGet 配置完成还原和构建后，最终上述构建与完整测试均通过。该两次环境失败不是代码失败。

## Astra 轻度最终核验

- 独立 `git diff --check` 退出码 0；仅有 LF/CRLF 工作区提示。
- 独立 Release 增量构建退出码 0，0 个警告、0 个错误；该结果不替代 Sol 首次构建记录的 245 个既有警告。
- 独立完整桌面测试在实际用户环境退出码 0，247 PASS、0 FAIL。首次沙箱运行仅发布版本 fixture 因无法读取 `CodexSandboxOffline` NuGet 缓存退出 1；授权读取实际用户环境缓存后该 fixture 亦通过，未判为代码回归。
- Diff、结构化 XAML 断言和模块所有权边界审查均通过，无 Core、缓存、解码或版本变更。
- 自动化不能替代真实播放器源图及 100%、150%、200% DPI 下的同源 A/B 视觉验收。

## 风险与后续

- 已知限制：源码回归与构建不能替代真实封面、不同显示缩放比例和窗口合成下的视觉验收；高质量重采样也不能补回上游原图本身不存在的细节。
- 交接目标：Astra 轻度最终核验；之后由主任务负责提交与 GitHub 推送。
- 回滚点：基线 `82e921a85182579367bf36ddf25fd2d2b10a9f72`。
