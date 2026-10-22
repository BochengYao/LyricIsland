# 设置 DPI、默认外观与 v3.3.12-beta 本地候选交接

## 2026-10-21 预览文字居中、公版歌词与 v3.3.14-beta 候选

- 预览文字层改为与岛体同高的独立覆盖 `Grid`，并与岛体使用相同的纵向位移；不再通过父容器居中后追加一半位移间接补偿，因此凹角、凸角及屏幕边缘距离变化时文字中心都与岛体中心一致。
- 主行改为 1868 年公版歌曲《Dreaming of Home and Mother》的英文原词 `Dreaming of home, dear old home`；副行使用本项目中文翻译 `梦回故乡，亲爱的故乡`，保留英文与中文字体的同时预览。
- 回归增加文字层高度/位移一致性以及英文原词、中文翻译存在性检查；完整桌面回归为 `281 PASS / 0 FAIL`。
- 新版本源与候选为 `3.3.14-beta` / `3.3.14.0`；`publish/current/` 共 8 个文件、24,622,237 bytes，未自动启动，且没有 `staging-v3.3.14-beta` 残留。
- `publish/current/LyricHover.App.dll` SHA-256：`08CEB4C869A6E9CF2A344AD93459DAAAB035AF039B53583C59FA9FE470B670D3`。
- `publish/current/LyricHover.Core.dll` SHA-256：`C57E255F656CBF4E490A04E5C641BDFD649D1127815484DD970A93CC492D255A`。
- App/Core 候选哈希均与本轮 Release 输出一致；旧 `v3.3.13-beta` 已归档。
- 本次仍未生成 MSIX、未提交或推送 Git、未上传 Store；真实 WPF 视觉需要确认两行文字在不同字号、凹角/凸角和屏幕边缘距离下保持居中。

## 2026-10-21 对齐修正与 v3.3.13-beta 候选

- 按最新视觉反馈，将歌词岛预览内层边距从 `16,10,16,12 DIP` 调整为 `16,0,16,12 DIP`：岛体顶部与圆角预览框顶部对齐，左右和底部仍保留安全距离。
- 缓存容量同行从固定顺排 `DockPanel` 改为四列 `Grid`；`512 MB` 保持左侧，`刷新歌词` 固定在最右列并显式 `HorizontalAlignment="Right"`，与其他设置选项右边界对齐。
- 静态回归增加顶部零边距、旧顶部边距不存在、刷新按钮最右列和右对齐约束；完整桌面回归结果为 `281 PASS / 0 FAIL`。
- `dotnet build LyricHover.sln -c Release --no-restore` 成功，0 错误、310 条既有 `CS0436` 等警告；发布脚本的 App Release 构建为 0 警告、0 错误。
- 新版本源与候选为 `3.3.13-beta` / `3.3.13.0`；`publish/current/` 共 8 个文件、24,622,249 bytes，未自动启动，且没有 `staging-v3.3.13-beta` 残留。
- `publish/current/LyricHover.App.dll` SHA-256：`A06344CC005660A9F11D7E90365C572D06AB8BE3E8317E734619CE2AC3093207`。
- `publish/current/LyricHover.Core.dll` SHA-256：`85B6808C0FCB999848F83C30ABF3BE8717E32CFBE4E7CD26929E9A3912B965CD`。
- App/Core 候选哈希均与本轮 Release 输出一致；旧 `v3.3.12-beta` 已归档到 `publish/archive/v3.3.12-beta/`。
- 本次仍未生成 MSIX、未提交或推送 Git、未上传 Store；真实 WPF 视觉需要在设置页确认顶部贴齐和缓存按钮右边界。

- 日期：2026-10-20
- 任务线程：桌面端岛屿、设置与交互
- 交接目标：Desktop UI & Interaction、Quality & Release
- 工作目录：`D:\AppleMusicDesktopLyrics`
- 基线提交：`b287506c34c60b714914ace420aeed1b5a50336d`（`main`；当前分支落后 `origin/main` 9 个提交）
- 结果提交：未提交、未推送；工作树包含多个线程此前留下的未提交 WIP，本轮未清理或回滚这些改动
- 允许范围：设置窗口、歌词岛外观默认值及兼容迁移、设置相关模块字体应用、桌面回归、版本和本地 `publish/current`

## 用户问题与结果

1. **其他电脑打开设置时字体模糊**
   - 新增 `LyricHover.App/app.manifest`，声明 `PerMonitorV2,PerMonitor` DPI 感知，并由 `LyricHover.App.csproj` 显式嵌入。
   - 设置窗口启用 `UseLayoutRounding`、`SnapsToDevicePixels`、Display 文本格式和 ClearType 文本渲染，避免 Windows 对整个窗口做低清晰度位图缩放。

2. **默认外观与 1.14 倍重新标定**
   - 新用户默认：字体缩放 `1.00`、默认凹角模式、凹角半径 `30 DIP`、凸角半径 `13.2 DIP`、凸角屏幕边缘距离 `0 DIP`。
   - `1.00` 倍的新实际基准字号为 `17.1 DIP`，等于旧基准 `15 DIP × 1.14`，因此用户截图中的旧 `1.14` 视觉大小现在显示为 `1.00`。
   - 设置 Schema 从 7 升到 8。Schema 7 使用 `旧缩放 × 15 / 17.1` 迁移；例如旧 `1.14` 迁移后约为 `1.00`，保持实际字号不跳变。更早 Schema 按旧绝对字号除以 `17.1` 换算。
   - 歌词、歌曲信息和进度时间均使用换算后的实际字号/视觉缩放；“恢复默认半径”恢复为 `30 / 13.2 / 0`。

3. **缓存区刷新按钮拥挤**
   - 按钮文案从“强制刷新当前歌词”改为“刷新歌词”，固定宽度 `118 DIP`，与 `512 MB` 输入行同排并位于其右侧。
   - 增加简中、繁中、英文、日文文案映射；刷新动作仍复用原 `refreshCurrentLyrics` 命令，没有修改 Core 缓存或歌词检索语义。

4. **歌词岛预览越过圆角安全区**
   - 预览岛体从容器四边内缩 `16,10,16,12 DIP`，最小预览高度从 `84` 降到 `68 DIP`。
   - 几何仍按内缩后的实际宽度重算，文字继续按岛体中心和凸角屏幕边缘距离补偿，不修改真实岛体几何算法。

5. **支持者姓名确认提示背景透明**
   - 根因是透明分层确认窗继承了主设置窗用于 Mica 的低 Alpha 根背景，但确认窗本身没有 Mica 托底。
   - 确认窗新增独立 `SupporterConfirmationBackgroundBrush`，从当前主题控件表面色生成 Alpha 255 的不透明背景；深浅主题文字、边框和按钮资源仍继承主设置窗。

6. **Enter 自动关闭设置窗口**
   - 移除底部“完成”按钮的 `IsDefault="True"`，普通 Enter 不再触发保存并关闭。
   - 支持者姓名输入框内的 Enter 仍由专用处理器拦截，只打开永久刻印确认，不关闭设置窗口。

## 本轮涉及文件

- `Directory.Build.props`
- `CHANGELOG.md`
- `LyricHover.App/LyricHover.App.csproj`
- `LyricHover.App/app.manifest`
- `LyricHover.App/OverlayPlacementSettings.cs`
- `LyricHover.App/PlacementSettingsWindow.xaml`
- `LyricHover.App/PlacementSettingsWindow.xaml.cs`
- `LyricHover.App/SupporterBadgeImprintConfirmationWindow.xaml`
- `LyricHover.App/SupporterBadgeImprintConfirmationWindow.xaml.cs`
- `LyricHover.App/UiLanguageService.cs`
- `LyricHover.App/Modules/LyricsModuleView.xaml.cs`
- `LyricHover.App/Modules/ProgressModuleView.xaml.cs`
- `LyricHover.App/Modules/TrackInfoModuleView.xaml.cs`
- `LyricHover.Tests/Program.cs`
- `publish/README.md`（本地发布目录说明）

这些文件中的一部分在本轮开始前已有其他未提交改动；后续提交必须按差异逐块审查，不要直接把整个脏工作树视为本任务独占改动。

## 自动验证

- 首次 `dotnet build LyricHover.sln -c Release` 未进入编译：沙箱无权读取 `C:\Users\14731\AppData\Roaming\NuGet\NuGet.Config`。
- 使用同一已还原依赖执行 `dotnet build LyricHover.sln -c Release --no-restore`：成功，0 错误、310 警告。警告为测试项目链接 LyricDock 类型与 App 程序集同名等现有 `CS0436`，本轮未新增编译错误。
- 首次完整桌面测试仅有发布事务夹具因 `C:\Users\CodexSandboxOffline\.nuget\packages` 不可读而失败；该夹具不覆盖本次 UI 行为。
- 设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE=1` 后执行 `dotnet run --project LyricHover.Tests -c Release --no-build`：`281 PASS / 0 FAIL`。
- `publish.ps1 -NoLaunch` 内再次执行同构建桌面测试，并完成 `win-x64` Release 构建与发布；App 发布构建为 0 警告、0 错误。
- `git diff --check`：通过，仅显示工作区既有 LF/CRLF 转换提示。

新增/更新回归覆盖包括：Schema 7 → 8 的 `1.14 → 1.00` 视觉等价迁移、新默认半径、Per-Monitor V2 manifest、预览安全边距、刷新按钮文案/宽度、刻印确认窗不透明背景，以及设置“完成”按钮不再是默认 Enter 动作。

## 本地候选与哈希

| 项目 | 当前证据 |
| --- | --- |
| 版本源 | `3.3.12-beta` |
| 文件/程序集版本 | `3.3.12.0` |
| 候选目录 | `publish/current/` |
| 文件数量 / 总大小 | 11 个文件 / 33,876,460 bytes |
| `LyricHover.App.dll` SHA-256 | `30C7F6ABB0FCE429A707889380B932E6A1B0FC70D47462477E08CC8B66BFB823` |
| `LyricHover.Core.dll` SHA-256 | `DCDC044CFC58E8167E2318520039938611728B0C3CE475BF96769D7FBCBC1A60` |
| 二进制一致性 | App/Core 哈希均与本轮 Release 输出一致 |
| 进程状态 | `publish/current` 候选未自动启动 |
| 新 staging | 没有 `staging-v3.3.12-beta` 残留 |

`publish/staging-diagnose-v3.1.39-Beta` 是 2026-08-24 留下的旧诊断目录，本轮未创建、未删除，不应误报为本次打包残留。

## 未修改 / 非目标

- 未改变 Core 播放器发现、歌词解析、歌词缓存格式、时间线或刷新命令语义。
- 未生成或更新 MSIX；`publish/current` 是框架依赖本地候选，不等于 Store 包。
- 未提交 Git、未推送 GitHub、未创建 GitHub Release、未上传或提交 Microsoft Store、未部署官网。
- 未清理当前工作树中其他桌面、Core、网站及文档 WIP。

## 实机验收仍待完成

自动测试和 manifest 静态检查不能代替真实 WPF/DPI 视觉验收。Desktop UI 线程应在真实 Windows 上至少验证：

1. 100%、125%、150%、200% 缩放下首次打开设置，中文/英文文字、下拉框和按钮是否清晰。
2. 两台不同 DPI 显示器之间移动设置窗口，确认 DPI 切换后没有位图模糊、尺寸异常、窗口跳位或文字裁切。
3. 新配置/删除旧配置后，外观初始值是否为 `1.00 / 30 / 13.2 / 凹角`；Schema 7 的旧 `1.14` 配置升级后实际视觉大小是否保持一致。
4. 点击“恢复默认半径”后，凹角、凸角和屏幕边缘距离是否分别回到 `30 / 13.2 / 0`。
5. 预览框在凹角/凸角、最小/最大字体缩放和长字体名下，尖角均保持在圆角矩形安全区内。
6. 缓存容量为三到四位数、四种 UI 语言下，“刷新歌词”按钮命中区和同行布局是否正常。
7. 深色/浅色/系统主题下输入支持者姓名，确认弹窗背景是否不透明且文字清晰。
8. 设置页各控件按 Enter 不关闭窗口；姓名输入框按 Enter 只打开刻印确认；点击“完成”仍能正常保存并关闭。

## 交接动作

- **Desktop UI & Interaction**：完成上述实机矩阵，尤其关注全局 Per-Monitor V2 对主岛、多屏原生定位和设置窗口移动的影响；记录截图、屏幕缩放、主副屏排列与结果。
- **Quality & Release**：提交前逐块审查混合 WIP，只纳入获准文件；若继续生成 MSIX，应重新解包核对包内 App/Core DLL 与上述候选或最终同构建输出的哈希，不能只看版本号。
- **Release/Store**：只有实机验收和最终范围审查通过后才可进入 MSIX/Store 流程；当前文档不代表已上传、已提交审核或已发布。

## 回滚点

- 旧 `publish/current` 已由发布脚本归档到 `publish/archive/v3.3.11-beta`。
- 如仅回滚默认视觉标定，必须同时回滚 Schema 8 迁移、三个模块的实际字号换算和设置预览基准，不能只把 UI 文本改回 `1.14`。
- 如回滚 DPI manifest，应重新验证设置窗口及主岛在混合 DPI/多屏上的清晰度和物理坐标，避免恢复到系统位图缩放。
