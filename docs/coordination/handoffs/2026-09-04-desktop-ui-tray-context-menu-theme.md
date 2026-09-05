# 任务交接：Desktop UI / 托盘右键菜单主题化

- 日期：2026-09-04（2026-09-05 补充 GitHub 交付状态）
- 任务线程：Desktop Island, Settings & Interaction UI
- 基线提交：`1de81933050c830d228a93069424ba038bb3fe5d`
- 功能提交：`f534f3a9e3f941d2b38a592261f2baba81710e4b`（紧凑桌面浮层视觉重设计）
- DPI 修复提交：`3de6fb9`（菜单整体布局按右键所在显示器同步缩放）
- 分支：`codex/feature/desktop-tray-menu-theme`
- 允许修改范围：`LyricHover.App/` 的托盘 UI 与直接回归测试
- Handoff Status：Integrated / 本地 `main` 与 GitHub `main` 已同步，待 UI 实机视觉确认

## 已完成

- 将默认 Windows `ContextMenuStrip` 渲染替换为 LyricHover 自定义托盘菜单渲染层。
- 菜单实测布局缩为 `160 x 77`：外边距 5px、菜单项 `150 x 33`、项目间距 1px；移除分割线，保留两个普通系统菜单项的层级。
- 外层圆角收敛到 10px、菜单项圆角 6px；Hover 始终与外框保留 5px，避免形成贴边大胶囊。
- 浅色采用 `#F8F8FA` / `#1D1D1F` / `#3A3A3C`，深色采用 `#242426` / `#F2F2F7` / `#D1D1D6`；Hover 与 Pressed 使用中性低透明度灰，不使用蓝色或红色强调。
- 为“偏好设置”和“退出”保留 `Segoe MDL2 Assets` 的 Gear / Power 矢量轮廓，但改为 `GraphicsPath` 归一化到相同 15px 光学尺寸后抗锯齿填充；两枚图标线性、同色、无背景容器。
- 文字使用 `Segoe UI Variable Text` 10pt Regular，通过灰度 `AntiAliasGridFit` 绘制，避免 ClearType 彩边；图标盒、8px 图文间距和文字基线统一对齐。
- Windows 11 优先请求 DWM 原生窗口圆角，避免二值 `Region` 导致外缘毛边并保留系统阴影；Windows 10 或 DWM 调用失败时安全回退到 10px 圆角 Region。
- 保留柔和系统下拉阴影与极弱 1px 主题边缘；未引入 Acrylic、Composition 或第三方依赖，采用需求允许的实体材质色回退。
- Apply 设置主题后立即刷新托盘菜单；跟随系统模式在每次菜单打开前重新读取 `AppsUseLightTheme`。
- 高对比度模式回退到 Windows `SystemColors`，避免自定义色破坏可访问性。
- 保留托盘双击打开设置、菜单打开设置和退出应用的原行为。
- 菜单项没有缩放、旋转、位移、发光或波纹动画；维持系统弹出行为，避免增加业务行为和 Windows 10/11 兼容风险。
- 用户 150% DPI 截图暴露固定 `160px` 容器与 DPI 字体/图标不一致：旧文字区域约 90px，而“偏好设置”实测约需 92.95px，因此出现 `偏好设...`。
- 在菜单 `Opening` 阶段通过鼠标所在显示器解析有效 DPI，并同步缩放菜单宽度、行高、Padding、Margin 与图标/文字布局；显示前完成布局，不引入可见尺寸跳变。DPI API 不可用时回退到 WinForms `DeviceDpi`。

## 未修改 / 非目标

- 未修改设置模型、持久化键、Core、播放器、歌词、缓存或时间线。
- 仅上传 GitHub 功能分支；未创建 GitHub Release、未提交 Microsoft Store、未发布官网或其他发行渠道。

## 本地候选

- 当前版本：`3.2.35-Beta`，framework-dependent `win-x64`；按用户要求使用 `-KeepVersion` 同版本重建。
- 目录：`publish/current`，11 个文件，共 33,792,278 bytes。
- `LyricHover.App.dll` SHA-256：`EA8A2A44DA18127340C5DCEEE084B61D4D4C8C5DFBBDCADE67B48CBDBDD2D2D4`。
- `LyricHover.App.exe` SHA-256：`1FF4A9D1DE3FC104F52BFD3A5C78237CBDAF02B71E679D994C37691BEEB173F7`。
- 前两轮 `3.2.35-Beta` current 已由权威脚本保留在 `publish/archive/v3.2.35-Beta*`；中间生成但未交付的 `3.2.36-Beta` 也保留在归档中供追溯。

## 验证

- 命令：设置 `TargetPlatformSdkPath` / `TargetPlatformDisplayName` 后运行 `dotnet build LyricHover.sln -c Release --no-restore`。
- 结果：成功，0 error；测试项目保留 190 条既有 `CS0436` / 未使用事件警告。
- 命令：设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 后运行 `dotnet run --project LyricHover.Tests -c Release --no-build`。
- 结果：全部执行项 PASS；源码契约和运行时菜单实例测试确认浅/深配色、紧凑尺寸、无分割线、矢量图标路径、菜单结构和主题刷新入口。
- DPI 回归：在 100% / 125% / 150% / 175% / 200% 缩放下生成两枚图标路径，检查边界有效且最大光学尺寸一致。
- 完整布局 DPI 回归：在 100% / 125% / 150% / 175% / 200% 下验证容器宽度、菜单项宽高和文字可用区按同一比例缩放；当前机器 150% 下文字可用区由约 90px 增至约 164px。
- 命令回归：运行时分别触发“偏好设置”和“退出”菜单项，原有回调各执行且仅执行一次。
- 命令：设置 Windows SDK 路径后运行 `publish.ps1 -KeepVersion -NoLaunch`。
- 结果：完整回归 PASS；`win-x64` Release 发布成功，发布脚本输出 `发布完成：v3.2.35 Beta`。
- 命令：`dotnet run --no-restore --configuration Release --project LyricHover.Tests -- --release-version-fixture`。
- 结果：在可读取真实用户 NuGet 缓存的环境中 PASS；发布版本变更保持事务化与串行化。
- 产物一致性：`publish/current/LyricHover.App.dll` 与同次 Release `win-x64` 输出 SHA-256 完全一致；deps/runtimeconfig 均非空，目标 staging 已清理。
- 离屏实渲染：浅色、深色预览均为 `160 x 77`，确认项目实际边界为 `{5,5,150,33}` 与 `{5,39,150,33}`，无分割线、无贴边 Hover、图标与文字未重叠。相较用户截图约 `203 x 95`，宽度缩小约 21%、高度缩小约 19%。
- Computer Use：尝试启动当前构建时，被正在运行的 `publish/current` 单实例正确拦截；托盘驻留窗口不向 Computer Use 暴露可绑定窗口，因此未冒险关闭用户当前实例或误操作其他窗口。

## 风险与后续

- 已知限制：离屏实渲染不能呈现真实桌面 DWM 阴影，也不能替代真实托盘弹出后的最终观感确认；正在运行的旧进程必须完全退出后再启动 `publish/current`，否则仍会显示内存中的旧菜单实现。
- 交接目标：Desktop UI / User Acceptance；从 `publish/current/LyricHover.App.exe` 启动，分别在浅色、深色、跟随系统和高对比度下右键托盘图标。
- GitHub 交付：用户已明确确认旧名仓库；分支 `codex/feature/desktop-tray-menu-theme` 已推送到 `https://github.com/BochengYao/LyricIsland.git` 并设置 upstream。远端分支包含托盘视觉重设计、150% DPI 裁切修复、自动化测试与本 Handoff。
- 回滚点：回滚 `305dbd82217a6d36f8923e5e1f860bc3f3e9cf4d` 即恢复默认 `ContextMenuStrip`。

## 主线集成（2026-09-05）

- 为保护根工作树内其他线程的未跟踪 WIP，在独立干净 Worktree `tray-menu-theme-integration-20260905` 中执行集成。
- 远端主线基线：`1a54d2bb27e14802137eb4e518093a26cea6c2cb`；托盘功能分支：`3ec5e4b59bda9e5968983f041d1e2c71cd67ff9d`。
- 合并提交：`a4e7b1e04a1dd2cb89175a4552ddcec2a182b0a0`；使用 `ort` 策略无冲突完成，并同时保留远端最新 Website 提交与本地既有 Desktop 主线提交。
- Integration Worktree 执行 `dotnet restore LyricHover.sln` 后，Release 构建成功（0 error，190 条既有测试警告），完整回归全部 PASS。
- 合并结果已验证同时包含原本地 `main`、最新 `origin/main` 与托盘功能分支；本地 `main` 和 GitHub `main` 已快进到包含集成证据提交 `672f63e` 的提交链，未重写远端历史。
