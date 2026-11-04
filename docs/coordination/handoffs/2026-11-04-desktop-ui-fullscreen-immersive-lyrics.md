# 全屏沉浸歌词实现与稳定性修复交接

- 日期：2026-11-04
- 工作目录：`D:\AppleMusicDesktopLyrics`
- 任务线程：全屏沉浸歌词连续实现与实机反馈修复
- 交接目标：桌面端岛屿、设置与交互
- 交接起点分支 / HEAD：`main` / `36772e63d0e2eae37f29f56f9b63269145806195`
- 交接起点状态：`main` 落后 `origin/main` 9 个提交；工作树包含多个线程的未提交 WIP，本任务未清理或回滚这些无关改动
- 外部边界：未上传商店、未创建 GitHub Release、未部署官网

## 已实现范围

### 全屏视图与共享播放状态

- 新增独立 WPF 全屏沉浸歌词窗口，复用现有 SMTC 会话、播放命令、歌词对象、共享时间轴、封面和设置状态，没有建立第二套播放器状态。
- 宽屏为封面 / 播放信息与连续歌词双栏，窄屏切换上下布局；支持 Esc、焦点恢复、显示器变化、淡入淡出和辅助控件自动隐藏。
- 封面使用圆角裁剪、轻柔阴影和高质量缩放；异步封面、在线高清封面升级和主题提取均绑定歌曲身份，避免快速切歌时旧结果覆盖。
- 播放进度、暂停 / 恢复、上一曲 / 下一曲、系统输出音量和设置入口均已接入现有命令链。

### 歌词渲染与交互

- 当前行持续居中；滚动、字号、透明度和颜色使用非线性动画，手动滚轮浏览后定时恢复自动跟随。
- 支持原文 / 翻译独立字体、字号、字重和行距；系统字体列表带中文名称与字体预览，预览、应用、取消、恢复默认和持久化继续走现有设置草稿流程。
- 逐字歌词只在真实逐字时间存在时启用；当前行内已播放文字为白色，未播放部分为亮灰，非当前歌词更暗且缩小。
- 歌词行使用真实透明点击层；播放器公开 `CanSeek=true` 时点击行发送该行准确时间戳，并短暂保持目标锚点，阻止旧 SMTC 样本立即回弹。
- 对所有播放器统一读取 `IsPlaybackPositionEnabled`。当 `CanSeek=false` 时，歌词行不显示手型光标、圆角悬停背景或按压反馈，点击层不参与命中；能力恢复后即时重新启用。

### 专辑主题背景与音频律动

- 背景由低分辨率可写位图渲染层、封面代表色、缓慢流动形变和歌词阅读暗部组成，没有持续模糊整棵 WPF 视觉树。
- 支持静态、氛围流动和系统输出音频能量响应；音频不可用时回退氛围流动，不把随机动画标成节奏同步。
- 音频分析仅在内存计算系统混音能量，不保存、不上传；系统输出包含其他应用声音时，也可能影响律动。
- 全屏隐藏或关闭后停止渲染、音频分析和事件订阅。

### 近期针对实机反馈完成的修复

- 修复全屏打开闪退、白闪、窗口按钮椭圆、专辑占位黑块、旧封面残留和低清封面被重复放大的问题。
- 修复歌词只在接近边缘时才校正、每两句对齐、上一句滑动结束后才缩小变色、逐字两行同时变亮及动画掉帧等问题。
- 修复播放 / 暂停 / 上下曲和菜单图标不居中、菜单不能再次点击收回、菜单突然出现以及设置卡片视觉不一致。
- 修复播放和音量轨道断裂、已播放 / 未播放之间留缝、`0:00` 仍出现白点，以及开始播放时因 `-6px` 补偿突然冒出圆点的问题。
- 进度条在 `0:00` 时白色宽度严格为 0；开始播放后关闭局部布局取整，白色部分按帧从左端连续变长。
- 修复歌曲标题先到、时长稍后由 `0` 补全时被误判为再次切歌的问题；元数据补全不再重置平滑时间轴或丢弃已发起的歌词加载结果。

## Apple Music / SMTC 实机证据

- 当前 Apple Music Windows 版：`1.1792.39682.0`。
- 当前会话：`AppleInc.AppleMusicWin_nzyj5cx40ttqa!App`。
- 实机读取结果：`IsPlaybackPositionEnabled / CanSeek = false`；因此 Apple Music 自己可拖动进度，但第三方不能通过通用 SMTC 精确定位。
- 同机另一播放器会话返回 `CanSeek=true`，证明能力读取链路正常。
- Apple Music 时间位置约每秒更新一次整数秒；应用在样本间使用共享单调时间轴平滑推进。歌曲启动期的额外回退已通过“未知时长补全不重置时间轴”修复。

## 关键文件

- 全屏窗口：`LyricHover.App/Fullscreen/FullscreenLyricsWindow.xaml(.cs)`
- 主题与背景：`AlbumThemeExtractor.cs`、`AmbientBackgroundControl.cs`、`LowResolutionAmbientBackgroundControl.cs`
- 封面处理：`ArtworkEnhancer.cs`、`OnlineArtworkResolver.cs`
- 音频分析：`SystemOutputAudioAnalyzer.cs`
- 字体与预设：`FullscreenLyricsSettings.cs`
- 组合根 / 时间轴 / 命令：`LyricHover.App/MainWindow.xaml.cs`
- SMTC 能力映射：`LyricHover.App/Media/SmTcMediaSessionService.cs`
- 共享能力模型：`LyricHover.Core/Media/MediaControlCapabilities.cs`
- 逐字渲染：`LyricHover.App/Modules/WordTrackingTextBlock.cs`
- 设置与快捷键：`OverlayPlacementSettings.cs`、`PlacementSettingsWindow.xaml(.cs)`、`HotkeySettings.cs`
- 回归：`LyricHover.Tests/Program.cs`、`LyricHover.Tests/FullscreenAnimationProbe.cs`

## 构建、测试和本地候选证据

| 项目 | 结果 |
| --- | --- |
| App Release 构建 | `3.4.0-beta` 通过，0 警告 / 0 错误 |
| 完整桌面回归 | 全部通过；发布版本事务测试单独通过 |
| 全屏专项回归 | 13 PASS / 0 FAIL |
| 最近新增覆盖 | `CanSeek=false` 交互禁用；精确歌词点击；旧 SMTC 位置保持；`0:00` 零宽度；亚像素连续增长；时长补全不重置时间轴 |
| 本地候选 | `publish/current/`，18 个文件，34,629,671 bytes |
| App DLL | 文件版本 `3.4.0.0`；产品版本 `3.4.0-beta`；SHA-256 `69496B469A396416013CE37EB87F1BA21272A5663454D87AD2DD3689BFC521B5` |
| Core DLL | 文件版本 `3.4.0.0`；SHA-256 `587978C4BCFA463F883D65B28ACCB688926652DCD528D88DECC02667B69F3C21` |
| 当前运行 | 打包前已关闭旧候选；新候选按 `-NoLaunch` 未自动启动 |

- 专项与完整测试在打包前独立运行通过；随后执行 `publish.ps1 -TargetVersion 3.4.0 -NoLaunch`，发布脚本再次运行完整桌面回归并成功生成候选。
- 本地候选为 `3.4.0-beta`；它只是框架依赖运行包，不是 MSIX、商店发布或外部渠道交付证明。

## 未完成的人工验收

自动测试不能代替真实 Windows 视觉和交互验收，接收线程仍需覆盖：

1. 使用支持 `CanSeek=true` 的播放器验证点击歌词确实改变播放器时间；Apple Music 应表现为无悬停圆角、无手型和不可点击。
2. 从 `0:00` 开始播放多首歌曲，确认进度白线从零连续增长，前几秒没有圆点突现、抖动或回退。
3. 对 1080p / 1440p / 4K、100%–200% DPI、双屏和显示器断开进行全屏进入 / 退出与焦点验收。
4. 对深色、浅色、黑白、高饱和及低清封面检查过渡、残影、裁剪、圆角、阴影和在线高清替换。
5. 试听静态、氛围流动、音乐律动、暂停收敛、恢复和音频设备切换；系统混音包含其他应用声音属于已知采集范围。
6. 覆盖长句、中英日混排、超大字号、缺失字体、翻译缺失、无歌词、长间奏、手动滚动后自动跟随。

## 接收线程注意事项

- 当前共享工作树包含大量网站、发布、设置和桌面 WIP；禁止整体 reset、checkout、清理或广泛暂存。
- `LyricHover.App/Fullscreen/` 当前为未跟踪目录；集成时必须显式纳入所需文件，避免只提交现有跟踪文件而漏掉全屏实现。
- 当前 `main` 落后 `origin/main` 9 个提交。合并或同步前先做差异审计，避免新主线覆盖本地全屏 WIP。
- 不要把本交接或 `publish/current` 当成 GitHub、Microsoft Store、ESA 或真实多屏视觉验收完成证明。

## 跨线程发送状态

- 本文件已生成，目标为“桌面端岛屿、设置与交互”线程。
- Codex Desktop 的 `list_threads`、`read_thread` 和 `send_message_to_thread` 均返回 `Invalid app tool request`，未将这些失败调用视为送达。
- 随后使用本机 `codex queue --thread "桌面端岛屿、设置与交互"` 发送同一交接路径与摘要，命令以退出码 0 完成；消息已成功加入目标线程队列。
