# 任务交接：歌词岛文字渲染清晰度

- 日期：2026-10-21—2026-10-22
- 任务线程：桌面端岛屿、设置与交互
- 基线提交：`b287506c34c60b714914ace420aeed1b5a50336d`（`main`；工作树包含多个既有未提交 WIP）
- 结果提交：未提交、未推送
- 允许修改范围：`LyricHover.App/` 歌词岛文字渲染、`LyricHover.Tests/` 对应回归、更新日志、版本和本地 `publish/current`

## 已完成

- 主歌词岛启用 `UseLayoutRounding`、设备像素吸附、`Display` 文本格式、`Grayscale` 渲染和固定文字 Hinting；保留透明窗口、现有字体设置、逐字跟随和动画。
- 原固定 `Y=-2.5 DIP` 模块位移改为按当前显示器 DPI 吸附到实际物理像素，并在窗口跨 DPI 后重新计算。
- 歌词居中的 Canvas 左坐标与跑马灯终点按当前横向 DPI 吸附到实际物理像素，避免静止终态落在半像素。
- `WordTrackingTextBlock` 的明暗双层文字显式使用同一透明背景文字模式，避免两层采用不同继承环境。
- 主行与副行的排版行高保持不变，但裁剪区域增加随字号增长的下伸空间；副行从主行扩展后的裁剪区域之后开始，避免自选英文斜体的 `y/f` 等下伸笔画被裁掉。
- 主行或副行超过可视宽度、需要跑马灯时，当前层与切换进入层都会在左右两端使用约 `10 DIP` 的短渐变遮罩；遮罩使用视口绝对坐标，不随歌词 `TranslateTransform` 移动；未溢出的居中歌词不设置遮罩。
- 修复模块宽度变化但歌词文本未变化时渐变遮罩沿用旧测量结果的问题；布局完成后会重新测量当前层与切换层，并按新宽度重建遮罩和跑马灯终点。
- 中文副行默认字号由主行的 `70%` 提升为 `75%`，文字不透明度由约 `85%` 提升为约 `93%`；设置页预览同步使用相同比例，保留现有行高、裁剪余量和灰阶抗锯齿。
- 新增回归覆盖 100%、125%、200% DPI 的像素吸附结果及主窗口、歌词居中、跑马灯和逐字文字渲染约束。

## 未修改 / 非目标

- 未修改字体或字号设置值、Schema、歌词解析、播放时间线、逐字进度、模块布局语义、Store 身份或数据迁移。
- 动画进行期间仍允许连续亚像素移动；修复针对静止排版与动画终点，不把动画离散成跳帧。
- 未生成 MSIX，未上传 Store，未提交或推送 Git。

## 验证

- `dotnet build LyricHover.sln -c Release --no-restore`：成功，0 错误；310 条为测试工程重复链接类型等既有警告。
- `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 下执行完整桌面回归：`289 PASS / 0 FAIL`；新增物理像素吸附、英文下伸空间、长歌词双侧短渐变、宽度/字号变化重测和中文副行可读性回归均通过。
- 首轮回归中相册封面静态测试仍要求旧的 `TranslateTransform Y="-2.5"`，已按新物理像素吸附契约更新断言；重跑通过。
- `tools/publish-next-version.ps1 -NoLaunch` 与最终 `-KeepVersion -NoLaunch`：成功，脚本内再次完成桌面回归、win-x64 Release 构建和发布，最终候选为 `v3.3.25 beta`。
- `publish/current/`：11 个文件、33,900,480 bytes；候选未自动启动，没有 `staging-v3.3.25-beta` 残留；替换前同版本候选已归档。
- `publish/current/LyricHover.App.dll`：FileVersion `3.3.25.0`、ProductVersion `3.3.25-beta`、SHA-256 `860BBA83C865D3D359A3FB1A498C7E74D18E3F698CBDA9EEA59782B1366F19F6`。
- `publish/current/LyricHover.Core.dll`：FileVersion `3.3.25.0`、ProductVersion `3.3.25-beta`、SHA-256 `F16A275887D3D1554B9BF0CD20417E77F4E2F218377A89CFA1DDAF3DE89F88FD`。
- App/Core 候选哈希分别与本轮 Release 输出一致；`git diff --check` 通过，仅显示既有 LF/CRLF 提示。

## 风险与后续

- 已知限制：自动测试不能证明真实 WPF 字形观感；需要在 100%、125%、150%、200% 缩放、静止歌词、切歌动画和长歌词跑马灯停止后进行实机视觉确认。
- 交接目标：Desktop UI / Quality 在真实 Windows 上比较修复前后截图，重点检查微软雅黑 UI、宋体以及用户自选字体。
- 回滚点：同时回滚主窗口文字模式、DPI 位移更新、歌词横坐标/跑马灯终点吸附和对应回归，不能只恢复固定 `-2.5 DIP`。
