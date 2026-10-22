# 桌面崩溃、外观设置与 v3.3.8 商店候选交接

- 日期：2026-10-19
- 任务线程：排查商店版右键崩溃
- 工作目录：`D:\AppleMusicDesktopLyrics`
- 基线提交：`b287506c34c60b714914ace420aeed1b5a50336d`（`main`；工作树含其他线程的未提交 WIP，本任务未整体提交或推送）
- 交接目标：桌面外观设置、质量与发布、歌词坞崩溃修复相关线程

## 已定位的崩溃

- 用户提供的 Microsoft Store `3.2.51.0` 两次崩溃（2026-10-19 21:03、21:08，机器 `qrj`）有相同的 .NET Runtime 1026 堆栈：`System.IO.IOException: 无法删除要被替换的文件` → `File.Replace` → `AtomicFileWriter.WriteAllText` → `OverlaySettingsStore.Save` → `SettingsRuntimeStateCoordinator.ApplyDraft` → `PlacementSettingsWindow.ApplyCurrentSettings` → `QueueDirtyStateUpdate`。事件 1000 的 `KERNELBASE.dll`/`0xe0434352` 本身不足以定位；1026 才证明两次同因。
- `LyricHover.Core/AtomicFileWriter.cs` 对 `File.Replace` 的 `IOException` 增加同目录覆盖移动回退和短暂重试；`PlacementSettingsWindow.xaml.cs` 在保存仍失败时捕获 IO/权限异常、显示提示并保留待保存修改，不因自动保存退出。锁定文件回归已加入 `LyricHover.Tests/Program.cs`。
- 此前还修复了右键歌词文字命中 WPF `Run` 时的父节点遍历、滑块调整时反复重建模块视图，以及拖放事件中同步更新布局可能造成的重入。相关改动分别在 `MainWindow.xaml.cs`、`IslandModuleHost.xaml.cs` 和 `DividerModuleView.xaml.cs`；不要仅凭不同用户的事件 1000 判断其他崩溃同因。

## 本轮外观设置改动

- `PlacementSettingsWindow.xaml(.cs)`：预览框改为撑满卡片宽度，几何按实际宽度重算，岛体顶部与预览框贴合；统一预览与字体行间距；数值单位右对齐；“恢复默认半径”移到半径设置下方；中英文字体下拉项按各自字体显示。
- 桌面 App 的 Release 编译通过，0 警告、0 错误；尚未记录真实 WPF 多 DPI/多屏视觉验收。

## 本地产物与平台状态

| 项目 | 状态 |
| --- | --- |
| 版本源 | `Directory.Build.props`：`3.3.8`，文件版本 `3.3.8.0` |
| 本地框架依赖版 | `publish/current/` 已更新并启动；App DLL SHA-256 `5EADFFA142BACB8F124BDDFD29F9A38DEF5ADFA49F3E5963FC6DE74487ABDACF`；Core DLL SHA-256 `77958E28208EB7A93281A2D0301A3DFC87FB39ABDF6B7EDF19B1789DF64E9081` |
| MSIX | `store/package/msix/LyricHover_3.3.8.0_x64.msix`，87,996,252 bytes，SHA-256 `48BD9B920A342B96CB0921ED11F18C5693066BC06CB66D1404FD7EBDA1112B9C`；已解包核对身份 `70643607.LyricIsland`、x64、版本及包内 App/Core DLL 与 `publish/current` 哈希一致 |
| 签名/Store | MSIX 未签名；未上传 Partner Center，未提交审核，未发布；用户仍可能运行 Store `3.2.51.0` |
| 商店文案 | `docs/release/store-v3.3.8-update-notes.md`，简中、繁中、英文、日文四段；每段开头有用户要求的强烈更新提醒 |
| Git/GitHub | 未为本任务提交、推送或创建 GitHub Release |

`3.3.8` 打包时使用 `publish.ps1 -SkipTests` 和 `store/msix/build-msix.ps1 -SkipTests -SkipRestore`：Release 构建与 MSIX 封装成功，本轮未运行自动测试。此前 `3.3.7` 崩溃修复候选的桌面回归为 276 PASS / 0 FAIL；此结果不能替代 `3.3.8` 回归。`publish/README.md` 已与 3.3.8 产物对齐。

## 待接续

1. 桌面 UI：实机复核预览、字体列表和半径按钮在 100%/150%/200% DPI、多屏及两种角型下的显示；检查下拉长字体名与缺字回退。
2. 质量与发布：在准备正式发布时，对当前工作树做范围审查、完成本版本回归及商店包实机验收；不要沿用 `3.3.7` 测试结果作为 `3.3.8` 证明。
3. Store：用户只要求生成包和四语文案，尚未授权上传或最终提交。微软商店的 mandatory 更新标记不会由系统强制执行；当前代码未实现 Store 更新检测与旧版阻断，不能承诺强制所有已安装用户立即更新。
4. 与网易云识别有关的报告未在此轮定位或修复，避免把它写成已解决。

## 跨线程发送状态

- 已从本地会话索引识别到“添加歌词岛自定义外观功能”“Quality & Release”“修复歌词坞开关导致崩溃”三个相关线程。
- 2026-10-19 尝试调用应用 `send_message_to_thread`，返回 `Invalid app tool request`；随后对上述三个 ID 使用 `codex queue`，均返回 `no rollout found for thread id`。**没有任何一次消息被确认送达**。
- 本文件作为可读取的交接记录保留；应用线程消息接口恢复后需重新发送，不能将文件写入视作已通知相关线程。
