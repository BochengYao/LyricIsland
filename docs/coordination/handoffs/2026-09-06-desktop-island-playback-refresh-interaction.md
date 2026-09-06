# 任务交接：歌词岛操控键、播放控制与完整歌词模块刷新范围

- 日期：2026-09-06。
- 任务：修复播放控制点击无响应、播放按钮误触发歌词刷新、暂停后疑似二次切换，以及歌词模块边缘无法刷新。
- Owner：Desktop Island, Settings & Interaction UI。
- 本轮起始基线：`c9ccc59fedeacc4e5070ac1bd1c41f82fe0a9de7`。
- 最终功能提交：`d78e57a85cccca8871b8ef400c9df85bd6ef3aa2`。
- 最后一次修复的父提交：`b51c7fbcddaa64bfa65bc2271079eda35c7ca023`。
- 工作分支：`codex/feature/desktop-playback-click-routing`；功能改动已快进进入本地 `main`。
- 状态：本地代码集成、自动化验证与候选生成完成；实机交互验收待确认。未推送本轮提交、未提交 Store 或正式发布。
- 允许范围：`LyricHover.App/`、相关 `LyricHover.Tests/` 测试和任务交接记录；用户明确允许本轮更新一次版本号。

## Result

最终交互约定以用户最后确认的行为为准：

| 场景 | 预期行为 |
| --- | --- |
| 正常运行，启用默认点击穿透选项，未按操控键 | 左键单击穿透歌词岛；播放按钮不可命中 |
| 按住已配置的临时操控键，默认 Ctrl | 暂时关闭鼠标光晕，允许点击岛上内容 |
| 操控键 + 单击歌词模块 | 整个歌词模块矩形，包括左右和上下空白，均可强制刷新歌词 |
| 操控键 + 单击上一曲、暂停/播放、下一曲 | 只发送对应播放控制请求，不触发歌词刷新 |
| 松开操控键 | 恢复正常鼠标避让和播放按钮门控 |
| 布局编辑 | 继续禁用播放按钮，保留原有布局编辑交互 |

模块之间的外部空隙、其他模块与全岛背景不因本次修复扩展为歌词刷新区域。现有点击穿透设置及水平拖动行为保留；“全岛穿透”描述针对正常左键单击，不能据此声称所有鼠标手势均为原生无条件穿透。

## Evidence 与实现

1. `MainWindow.Window_MouseLeftButtonDown` 优先识别按钮，歌词刷新继续要求操控键按下且事件源属于 Lyrics 模块；调用既有 `RefreshCurrentTrackLyrics(true)`。
2. `IslandModuleHost.SetPlaybackInteractionEnabled` 随操控键状态更新，播放按钮启用条件为临时交互开启且未处于布局编辑；布局重建时保留该状态。
3. `PlaybackControlsModuleView` 使用 WPF `Click` 作为播放请求的唯一派发入口。旧实现曾在 `PreviewMouseLeftButtonUp` 手工派发并保留 `Click` 路径，存在一次点击切换两次播放意图的风险；已删除额外派发路径。该证据证明 UI 重复派发路径已移除，不等于已证明所有真实播放器的延迟恢复现象均已消失。
4. 歌词模块根控件原来没有命中背景，空白区域会命中外层 Grid，沿事件源父级查找不到 Lyrics 模块。`LyricsModuleView.xaml` 增加 `Background="Transparent"`，让完整模块边界参与命中，不改变可见背景颜色。

## Commit SHA 与 Changed Files

提交链按先后顺序如下。接收方应复核最终状态，不要单独恢复早期“播放控制可直接点击”的实现。

| 提交 | 内容 |
| --- | --- |
| `27a686c1b5074353d14639a5c1097335b37710a1` | 初始播放按钮点击修复；其直接点击策略已由后续提交更正 |
| `8b046089bc95fe5dfe8afe8360cc5a363b85985a` | 用户授权的一次版本更新，`3.2.36-Beta` |
| `467e8b17e126c73c3980c820e811d4224a10da0f` | 播放按钮与歌词刷新路由分离 |
| `42dcec0ad3464a5b13a63af311c82a5c5bb3cc0f` | 移除鼠标抬起阶段重复派发 |
| `b51c7fbcddaa64bfa65bc2271079eda35c7ca023` | 恢复操控键门控、穿透条件和教程文案 |
| `d78e57a85cccca8871b8ef400c9df85bd6ef3aa2` | 完整歌词模块命中背景及坐标回归 |

本轮功能提交累计涉及：

- `Directory.Build.props`：唯一版本源，版本只更新一次。
- `LyricHover.App/MainWindow.xaml.cs`：临时交互、歌词刷新、播放点击、穿透路由及教程文案。
- `LyricHover.App/Modules/IslandModuleHost.xaml.cs`：模块归属判断和播放控件命中门控。
- `LyricHover.App/Modules/PlaybackControlsModuleView.xaml.cs`：播放请求单次派发。
- `LyricHover.App/Modules/LyricsModuleView.xaml`：完整模块透明命中背景。
- `LyricHover.Tests/Program.cs`：交互门控、模块路由、单次派发与坐标命中回归。
- `docs/testing/lyrics-module-hit-area-2026-09-06.md`：最后一次范围修复的验证证据。

## Verification

最终功能 SHA `d78e57a` 的 Release 构建退出码 0，0 error；测试项目保留 190 条既有类型冲突等警告，它们属于技术债，不是功能失败豁免。

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet build LyricHover.sln -c Release --no-restore -v:q /clp:ErrorsOnly
$env:LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE='1'
& .\LyricHover.Tests\bin\Release\netcoreapp3.1\LyricHover.Tests.exe
```

- 最终构建后执行的桌面套件：220 PASS、0 FAIL；本次单独跳过版本变更 fixture。
- `b51c7fb` 内容状态的前轮验证为 219 项桌面测试，另加独立 `--release-version-fixture` 1 项通过。不能将该历史 fixture 计入最终 SHA 的已执行数量。
- 新增坐标测试先测量并排列真实 WPF 控件，再用 `VisualTreeHelper.HitTest` 检查 240、680、1000 三种宽度，空/短/长歌词，各九个点，共 81 个歌词区域命中点；同时检查相邻播放区域不属于 Lyrics。
- 反向验证：移除透明背景，同一测试在宽度 240 的 `(0.5, 0.5)` 处失败，命中外层 Grid；恢复修复后通过。
- 其他测试覆盖播放按钮门控、布局编辑禁用、操控键教程，以及 PreviewMouseLeftButtonUp 不派发而 Click 派发一次。
- 以上属于自动化控件/视觉树与源码约束证据，不是桌面原生输入、DPI 实机截图或真实播放器会话验收。

## 本地候选与发布状态

- 使用 `publish.ps1 -KeepVersion -NoLaunch` 重建，退出码 0，旧候选由脚本归档。
- 版本：`3.2.36-Beta`，framework-dependent `win-x64`。
- 产物：`D:\AppleMusicDesktopLyrics\publish\current\LyricHover.App.exe`。
- DLL 构建时间：2026-09-06 10:23:17，本机时区 Asia/Shanghai。
- `LyricHover.App.dll` SHA-256：`42DF3874C52BF5871B6D72E0CB3BD34CFEF570C083BCF97DD83800669A465AC4`。
- 本交接前复核进程 PID 6660，启动时间 2026-09-06 10:23:51，路径确认为上述 `publish/current`。PID、运行状态和 current 均为时点证据，接收方需重新核对。
- 同版本曾重建多次，不能只凭版本号判断是否运行最新修复；请核对 DLL 指纹。
- 截至本记录前，本地 `main` 相比已缓存的 `origin/main` 超前 6 个功能/版本提交；未执行远端刷新，本轮没有推送、MSIX 提交、官网部署或正式发行。

## Cross-module Impact 与接收方行动

| 接收职责 | Impact | Suggested Owner / 后续 |
| --- | --- | --- |
| 桌面 UI | 操控键、模块命中、按钮请求与教程行为 | 吸收最终交互约定，复核完整模块边缘及自定义操控键 |
| 歌词与播放器核心 | UI 请求入口更正；未修改 Core、媒体会话、时间线或缓存语义 | Knowledge Sync；若实机仍自动恢复播放，结合目标会话和播放意图时序定位，再定义独立修复任务 |
| 测试与发布 | 同版本候选已替换，实机验收未闭环 | 按最终 SHA 与 DLL 指纹验收；保留自动化与实机证据层级，未验收不标记正式可发布 |
| Architecture | 本地实现修复及用户交互要求落实，无新增跨域接口 | Knowledge Sync，评估领域知识影响和验收缺口；不承担中央调度，无需重复集成或据此创建新 ADR |
| 复盘测试当前版本 | 正在独立复核同一功能 SHA | 将本轮命中范围、单次派发证据并入当前复盘，保留独立测试结论 |

## Known Limitations / 实机验收清单

1. 在实际播放器播放中，不按操控键，分别点击歌词中心、四角、边缘及播放按钮位置，确认底层窗口收到预期单击，岛内不刷新或切换播放。
2. 按住 Ctrl，确认光晕暂时关闭；点击歌词模块完整区域均刷新；点击相邻其他模块/间隙不刷新。
3. 按住 Ctrl，分别操作上一曲、暂停/播放、下一曲，确认每次操作只生效一次、不会触发歌词刷新；暂停后观察至少 10 秒并核对播放器真实状态。
4. 松开 Ctrl 后验证按钮重新不可命中；覆盖设置的其他操控键、左右 Ctrl、布局重建及布局编辑。
5. 覆盖空/短/长歌词、不同模块宽度及 100%/150% DPI；如出现误判，提供坐标、配置、播放器和产物指纹。

回滚参考：最后一次命中范围补丁的父提交为 `b51c7fb`，旧包保存在 `publish/archive`；恢复该父版本会重现边缘刷新问题。不得直接恢复早期直接点击/重复派发版本作为完整修复。

## 分发记录

- 用户已明确要求“写交接记录发送给相关线程”。
- 已通过应用线程列表及最近状态确认五个目标会话：Desktop UI & Interaction、Desktop Core、Quality & Release、Architecture、复盘测试当前版本。
- Computer Use 初始化失败后，已确认原生 `list_threads` / `read_thread` 接口可调用；使用 `send_message_to_thread` 发送交接消息，回执单独记录。消息发送成功不等于已完成领域复核。
- 编写期间其他线程向 main 增加了独立文档提交；本交接采用单独文档提交集成，保留其他线程文件。候选功能 SHA 和上述产物指纹不因文档集成而改变。
