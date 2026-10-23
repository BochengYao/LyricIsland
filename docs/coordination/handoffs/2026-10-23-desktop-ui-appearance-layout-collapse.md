# 歌词岛外观、拖拽与布局编辑交接

- 更新时间：2026-10-24（Asia/Shanghai）
- 工作目录：`D:\AppleMusicDesktopLyrics`
- 当前分支：`main`
- 远端基线：`f9d27ad`（本地 `HEAD`、`origin/main`、`origin/HEAD` 一致）
- 最新本地候选：`v3.3.29-beta`
- 当前状态：源码和本交接文档已纳入本地 Git 提交；尚未推送；本地候选已生成
- 责任域：Desktop UI & Interaction / 歌词岛外观、模块布局与教学流程

## 本轮结论

### 已提交并推送的基础修复

- `d908dbb feat(ui): refine pro typography and lyric rendering`
  - 歌词岛与歌词坞中英文字体、字号控制及 Pro 权益联动。
  - 中文字体筛选、字体显示名、混合文字回退、DPI 文本清晰度、英文下行部与副行间距。
  - 超长歌词左右短渐变固定在歌词岛视口两端。
- `f9d27ad fix(ui): stabilize island shape and module dragging`
  - 凹角/凸角设置项对齐、凸角专属行非线性收起与复用滑杆动画。
  - 凹角切换为凸角时，屏幕边缘距离引起的位置变化与岛体尺寸共用同一帧动画，避免先跳位置再变形。
  - 模块布局改为增量协调，不再清空整棵视觉树；拖入、拖出、排序复用单次尺寸动画。
  - 新模块拖入期间合并透明窗口缩放引发的瞬时 `DragLeave`，降低缩回后再次展开的抖动。
  - 应用私有拖拽格式并提供透明 OLE/Shell 拖拽图像，避免系统白色文件/禁止拖放图标覆盖自定义跟随预览。

### 本次本地 Git 提交包含的修复

- 新拖入的分割线不再使用 `0.22 / 4 DIP` 默认样式；`LayoutEditSession.Add` 会从当前布局已有分割线继承：
  - `DividerOpacity`
  - `MarginBefore`
  - `MarginAfter`
- 教学模式移除“新功能”字样：
  - 删除“（新功能！）”强调文字。
  - “现在我们来体验新功能——自定义模块”调整为“现在我们来体验自定义模块”。
  - 同步调整繁体中文映射；英文、日文原本未包含该字样。
- `Directory.Build.props` 已由发布脚本递增到 `3.3.29-beta`。

## 当前修改文件

- `CHANGELOG.md`
- `Directory.Build.props`
- `LyricHover.App/MainWindow.xaml.cs`
- `LyricHover.App/UiLanguageService.cs`
- `LyricHover.Core/Layout/LayoutEditSession.cs`
- `LyricHover.Tests/Program.cs`
- `docs/coordination/handoffs/2026-10-23-desktop-ui-appearance-layout-collapse.md`

不要纳入提交：

- `.release-worktrees/`

## 自动验证

- `git diff --check`
  - 通过；仅有仓库既有的 LF/CRLF 转换提示。
- `dotnet build LyricHover.sln -c Release --no-restore`
  - 成功，`0` 错误。
  - 测试工程仍有既有的重复链接类型 `CS0436` 警告，不是本轮新增编译错误。
- 完整 Release 回归
  - `296 PASS / 0 FAIL`。
  - 新增覆盖：新分割线插入第一帧继承已有分割线透明度与两侧间距。
  - 教学回归明确断言源码不再包含“（新功能！）”和“体验新功能——自定义模块”。
- `tools/publish-next-version.ps1 -NoLaunch`
  - 成功生成 `v3.3.29-beta`，应用未自动启动。

## `publish/current` 证据

- 路径：`D:\AppleMusicDesktopLyrics\publish\current`
- 文件版本：`3.3.29.0`
- 文件数量：`11`
- 总大小：`33,911,120 bytes`
- `LyricHover.App.exe` SHA-256：`EDA09639E360B84C35EDFD03247B14FAEE769DB1343C2FF4806A2DE083C94D19`
- `LyricHover.App.dll` SHA-256：`EAB66F8C0401771765E9C51E8F91975DE37F0C8489DB261E653D7453073C53CE`
- `LyricHover.Core.dll` SHA-256：`058E2F88658C899C4D6C7D4BB6EEB6763CFBD4711FE13765371965C34C55097B`
- App/Core 与同次 Release 输出哈希一致。
- 旧候选归档：`publish/archive/v3.3.28-beta-20261024-000119/`
- `staging-v3.3.29-beta` 残留：`0`
- 打包后 `LyricHover.App` 进程数量：`0`

## 仍需真实 Windows 验收

自动测试无法替代以下实机检查：

1. 在 100%、125%、150%、200% DPI 下检查中英文歌词边缘清晰度、英文下行部和副行间距。
2. 多次执行凹角 → 凸角与凸角 → 凹角，确认位置和形状连续、无单帧抖动或矩形闪帧。
3. 从设置页连续拖入模块，确认不再出现白色文件图标，岛体不先缩回再展开。
4. 拖出、排序模块并快速穿过岛体边缘，确认瞬时 `DragLeave` 不触发可见回弹。
5. 调整现有分割线透明度和间距后再拖入新分割线，确认第一帧立即保持同样样式。
6. 重新运行教学模式，确认所有语言下均无“新功能”角标或相关字样，步骤和下一步按钮行为不变。

## Git 与发布边界

- 本轮提交前基线为 `f9d27ad`，且当时与 `origin/main` 一致；本轮提交是它的本地后继提交。
- 上述 `v3.3.29-beta` 相关源码和本交接文档已纳入同一个本地提交，尚未推送。
- 本次提交精确包含“当前修改文件”列表，并排除了 `.release-worktrees/`。
- `publish/current` 是本地 framework-dependent 测试候选，不是 GitHub Release。
- 未生成 MSIX，未上传或提交 Microsoft Store，未进行外部生产发布。

## 建议接续顺序

1. 先完成上述真实 WPF/DPI/拖拽验收。
2. 若仍能复现概率性抖动，记录操作路径、DPI、角型、窗口位置及录屏帧，不要仅依据自动测试宣布关闭。
3. 验收通过后，如需上传 GitHub，再推送当前本地提交并核对远端分支哈希。
4. 如需 MSIX 或 Store 后续动作，单独记录各渠道状态与哈希，不能把本地 `publish/current` 等同于已发布。
