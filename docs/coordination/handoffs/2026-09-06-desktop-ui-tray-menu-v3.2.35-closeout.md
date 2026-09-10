# 任务交接：Desktop UI / 托盘菜单 3.2.35 主线与 GitHub 收口

- 日期：2026-09-06
- 功能任务：`优化托盘右键菜单样式`
- 任务线程：Desktop Island, Settings & Interaction UI
- 基线提交：`1de81933050c830d228a93069424ba038bb3fe5d`
- 功能提交：`f534f3a9e3f941d2b38a592261f2baba81710e4b`、`3de6fb9`
- 主线集成提交：`a4e7b1e04a1dd2cb89175a4552ddcec2a182b0a0`、`672f63e`、`b49b110964591d3d69e4ef05f07e4e928cdee945`
- 历史资料归档提交：`06a88fd02224566ad3f66990ae4e905a9bfe18b4`
- 允许修改范围：`LyricHover.App/` 托盘菜单 UI、直接回归测试、版本文件、发布候选和本交接记录
- Handoff Status：Integrated / GitHub Delivered / Store Not Submitted

## 已完成

- 将托盘右键菜单从 Windows 默认 `ContextMenuStrip` 视觉替换为 LyricHover 自定义渲染层，保留“偏好设置”和“退出”的原命令绑定、回调、托盘生命周期及退出逻辑。
- 菜单重构为紧凑桌面浮层：约 `160 × 77`、10px 外层圆角、6px 菜单项圆角、33px 行高、5px 安全边距与 1px 项间距；无分割线、无贴边大胶囊 Hover。
- 浅色、深色、跟随系统和高对比度均有对应颜色路径；Hover / Pressed 使用中性低透明度灰，不使用蓝色或危险红强调。
- “偏好设置”与“退出”使用同色、同光学尺寸的 Gear / Power 矢量轮廓；图标和文字统一基线、间距与抗锯齿策略。
- Windows 11 优先使用 DWM 原生圆角和系统阴影，Windows 10 或调用失败时回退为圆角 Region；未增加 Acrylic、Composition 或第三方依赖。
- 菜单打开前根据鼠标所在显示器同步缩放容器、行高、Padding、图标和文字布局，覆盖 100% / 125% / 150% / 175% / 200% DPI，修复 150% 下“偏好设置”被截断的问题。
- 版本按用户要求保持并发布过 `3.2.35-Beta` 本地候选；最终对应候选保存在 `publish/archive/v3.2.35-Beta-20260905-171456/`。
- 功能分支已无冲突合入 `main`，集成记录已进入远端；`f534f3a`、`3de6fb9`、`a4e7b1e`、`672f63e`、`b49b110` 均为当前本地 `HEAD` 与 `origin/main` 的祖先。
- 用户另行明确授权将 366 个历史项目文件提交并公开推送；归档提交 `06a88fd` 已进入 GitHub `main`。该提交包含约 60.3 MB 的历史 Handoff、软著材料、源码快照、PDF、截图和运行时文件，不属于托盘菜单业务实现。

## 修改文件

- `LyricHover.App/TrayContextMenu.cs`：新增托盘菜单的主题、布局、DPI、矢量图标、绘制和兼容回退实现。
- `LyricHover.App/MainWindow.xaml.cs`：将托盘入口接入自定义菜单，并在主题变化与菜单打开前刷新视觉状态；原有命令处理保持不变。
- `LyricHover.Tests/Program.cs`：增加菜单结构、主题、DPI、图标边界和命令回调回归覆盖。
- `Directory.Build.props`：任务期间将版本更新并保持为 `3.2.35-Beta`；当前主线之后已由其他任务推进到 `3.2.36-Beta`。
- `docs/coordination/handoffs/2026-09-04-desktop-ui-tray-context-menu-theme.md`：原始实现、验证和主线集成明细。
- `docs/coordination/handoffs/2026-09-06-desktop-ui-tray-menu-v3.2.35-closeout.md`：本次最终收口记录。

## 未修改 / 非目标

- 未修改设置数据模型、持久化键、Core、播放器连接、歌词解析、缓存、时间线或应用关闭语义。
- 未创建 GitHub Release，未上传或提交 Microsoft Store，未修改官网生产路由；Git 推送不能表述为产品已发布。
- 历史资料归档提交 `06a88fd` 与托盘视觉实现相互独立，不应作为功能提交或 `3.2.35-Beta` 发布候选内容计算。
- 本交接不接管 2026-09-06 当前本地 `main` 上尚未推送的播放器点击路由工作。

## 视觉差异

- 修改前：Windows 原生白色菜单或大圆角卡片，行高与图标偏大，Hover 接近整行胶囊，图标/文字存在毛边、色彩和比例不一致。
- 修改后：克制的 macOS contextual-menu 风格桌面浮层，尺寸缩小约 19%～21%，圆角、留白、图标光学尺寸和文字基线统一；Hover 更轻，深浅色层级更稳定。

## 构建、测试与产物

- Release 构建：`dotnet build LyricHover.sln -c Release --no-restore` 成功，0 error；保留 190 条既有测试警告。
- 桌面回归：`dotnet run --project LyricHover.Tests -c Release --no-build` 全部执行项 PASS。
- 发布脚本：`publish.ps1 -KeepVersion -NoLaunch` 完成，输出 `发布完成：v3.2.35 Beta`。
- 版本事务夹具：`dotnet run --no-restore --configuration Release --project LyricHover.Tests -- --release-version-fixture` PASS。
- 最终 `3.2.35-Beta` 归档：`publish/archive/v3.2.35-Beta-20260905-171456/`，递归共 11 个文件、33,792,278 bytes。
- `LyricHover.App.exe` SHA-256：`1FF4A9D1DE3FC104F52BFD3A5C78237CBDAF02B71E679D994C37691BEEB173F7`。
- `LyricHover.App.dll` SHA-256：`EA8A2A44DA18127340C5DCEEE084B61D4D4C8C5DFBBDCADE67B48CBDBDD2D2D4`。
- 说明：截至 2026-09-06，当前 `Directory.Build.props` 和 `publish/current` 已由后续无关任务推进为 `3.2.36-Beta`；验证 `3.2.35-Beta` 时必须使用上述明确归档目录，不能把当前目录改名代替。

## 当前 Git 状态快照

- 核验日期：2026-09-06。
- 远端：`origin = https://github.com/BochengYao/LyricIsland.git`。
- 远端 `origin/main`：`c9ccc59`，已包含托盘主线收口提交 `b49b110` 与历史资料归档提交 `06a88fd`。
- 取证开始时本地 `HEAD` 为 `d78e57a`，比 `origin/main` 超前 6 个提交；这些提交属于后续播放器点击路由任务，不属于本交接，也未由本任务推送。
- 写入本记录期间，共享根工作树被其他任务并发推进到 `8c247a9`（比 `origin/main` 超前 7 个提交），并新增 `docs/testing/2026-09-06-current-version-review.md`；该提交和文件均不属于本交接，本任务未修改或纳入它们。
- 工作区在创建本记录前为 clean；本任务只新增本交接文件。接手时仍须重新刷新状态，因为其他活跃任务可能继续推进共享根工作树。

## 风险与后续

- 已知限制：自动化与离屏渲染不能替代真实托盘弹出时的 DWM 阴影和最终主观观感；验收前需完全退出旧单实例，再从指定候选启动。
- 公开资料风险：`06a88fd` 已按用户明确授权推送到公开 GitHub 仓库，历史软著材料、源码快照、二进制、PDF 与截图因此可被外部访问；若后续改变公开策略，应由 Brand & Compliance / Release 单独评估历史清理与凭据轮换，不能把普通 revert 当作撤回既有公开传播。
- 下一步 1（Desktop UI）：使用明确的 `3.2.35-Beta` 归档候选，在浅色、深色、跟随系统、高对比度和高 DPI 环境完成真实托盘弹出验收。
- 下一步 2（Quality & Release）：若要继续发版，以当前真实版本 `3.2.36-Beta` 重新构建并记录新哈希；不得复用本记录中的 `3.2.35-Beta` 哈希冒充当前产物。
- 下一步 3（Brand & Compliance）：确认公开仓库中的历史软著与源码快照是否符合长期披露策略，并记录处置决定。
- 待用户回答：无；本次交接仅记录已完成事实和明确后续责任，不替相关领域作发布或合规决策。
- 交接目标：`Desktop UI & Interaction`、`Quality & Release`、`Brand & Compliance`。
- 回滚点：托盘菜单视觉可从 `305dbd82217a6d36f8923e5e1f860bc3f3e9cf4d` 所代表的默认 `ContextMenuStrip` 基线恢复；任何回滚都应先在当前主线新提交中实施，不重写远端历史。
- 接手要求：本记录是 2026-09-06 的证据快照，不代表未来当前状态；接手后必须先运行 `git status --short --branch`、`git log -5 --oneline --decorate` 并核验目标产物，不得直接按旧 SHA 或旧 `publish/current` 推进。
