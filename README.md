# LyricHover

[English](#english) · [下载](https://apps.microsoft.com/detail/9NRXZP5HMXK2) · [官网](https://lyric-island.top/) · [反馈](https://lyric-island.top/incentives/) · [Issues](https://github.com/BochengYao/LyricIsland/issues)

> 这一句，值得被看见。

LyricHover 是一款为 Windows 10/11 打造的桌面歌词工具。音乐响起，歌词从屏幕顶端自然浮现；播放结束，LyricHover 完整收起。它通过 Windows 原生媒体会话连接你正在使用的播放器，从多个歌词来源匹配同步歌词与翻译，并把歌词、封面、歌曲信息、播放控制和进度组合成一座真正属于你的歌词岛。

QQ 音乐、网易云音乐、酷狗音乐、酷我音乐、Spotify 以及 Apple Music，都可以在同一个轻盈、连贯的桌面体验中被识别和切换。

![LyricHover 主视觉](视觉宣传/zh/1.png)

**[前往 Microsoft Store 下载 →](https://apps.microsoft.com/detail/9NRXZP5HMXK2)**

## 核心功能

- **始终在场，不打断工作。** 歌词贴合屏幕顶边出现，需要时展开，不播放时自然收起。
- **换个播放器，歌词照常在场。** 自动识别当前音乐播放器，也可以优先选择你常用的播放器。
- **想怎么展开，就怎么呈现。** 六种模块、两种布局、独立的紧凑与展开组合，都可以直接在真实歌词岛上调整。
- **鼠标靠近，内容仍是主角。** 只淡化指针附近的区域，让下方内容保持可读、可操作。
- **歌词与翻译更可靠。** 多来源自动回退、智能翻译复用、稳定缓存与时间轴补偿共同减少缺词、闪烁和不同步。
- **歌词坞 / Lyric Dock。** 在 Windows 任务栏显示实时歌词：支持左对齐/居中、长歌词跑马灯滚动，与歌词岛共享同款切换动画。
- **主体功能始终免费。** 你可以自愿加入 LyricHover Pro 支持计划，帮助 LyricHover 继续生长。

## 视觉预览

<table>
  <tr>
    <td width="50%"><img src="视觉宣传/zh/2.png" alt="鼠标智能避让" /></td>
    <td width="50%"><img src="视觉宣传/zh/3.png" alt="模块化歌词岛" /></td>
  </tr>
  <tr>
    <td align="center"><strong>鼠标靠近，内容仍是主角。</strong></td>
    <td align="center"><strong>想怎么展开，就怎么呈现。</strong></td>
  </tr>
  <tr>
    <td width="50%"><img src="视觉宣传/zh/4.png" alt="自动展开与收起" /></td>
    <td width="50%"><img src="视觉宣传/zh/6.png" alt="多播放器支持" /></td>
  </tr>
  <tr>
    <td align="center"><strong>一开场，就在场。</strong></td>
    <td align="center"><strong>换个播放器，歌词照常在场。</strong></td>
  </tr>
</table>

## 歌词坞 / Lyric Dock

- 在 Windows 任务栏安全空隙区域显示实时歌词。
- 支持左对齐和居中两种文本对齐方式。
- 长歌词自动从左缘开始跑马灯滚动，显示完整内容。
- 与歌词岛共享同款双面板淡入淡出 + 滑动切换动画。
- 单行模式垂直居中显示。
- 自动隐藏小组件以获取更大显示空间（Windows 11）。

## 播放器兼容性

| 播放器 | 媒体会话识别 | 说明 |
|---|---:|---|
| Apple Music | ✓ | 支持歌曲信息、封面、播放状态、时间轴与播放控制。 |
| QQ 音乐 | ✓ | 支持已发布到 Windows 媒体会话的信息与控制能力。 |
| 网易云音乐 | ✓ | 正常播放、切歌、歌词显示和控制可用；播放器内拖动进度无法实时同步歌词。 |
| 酷狗音乐 | ✓ | 支持已发布到 Windows 媒体会话的信息与控制能力。 |
| 酷我音乐 | ✓ | 支持已发布到 Windows 媒体会话的信息与控制能力。 |
| Spotify | ✓ | 支持已发布到 Windows 媒体会话的信息与控制能力。 |

## 默认全局快捷键

| 快捷键 | 功能 |
|---|---|
| `Ctrl` | 临时启用交互并展开自动折叠布局 |
| `Ctrl+Alt+Left` | 歌词提前 500 ms |
| `Ctrl+Alt+Right` | 歌词延后 500 ms |
| `Ctrl+Alt+Down` | 重置歌词时间偏移 |

所有快捷键都可以重新绑定。

## 歌词来源与隐私

LyricHover 支持 LRCLIB、QQ 音乐、酷狗音乐和网易云音乐等歌词来源，并只会发送匹配歌词所需的歌曲标题、歌手、专辑和时长等信息。歌词内容与接口由相应第三方提供，使用时请同时遵守其服务条款。

设置、歌词缓存与 Pro 验证结果保存在本地 `%LOCALAPPDATA%\LyricHover\`。程序会自动迁移旧版设置与歌词缓存。桌面端不要求注册账号。

## 从源码运行

环境要求：Windows 10/11 x64、Git、能够构建 `netcoreapp3.1` WPF 项目的 .NET SDK、Windows 10 SDK `10.0.19041` 或兼容版本。

```powershell
git clone https://github.com/BochengYao/LyricIsland.git
cd LyricIsland
dotnet restore LyricHover.sln
.\run.ps1
```

### 构建与验证

```powershell
dotnet restore --runtime win-x64 LyricHover.App\LyricHover.App.csproj
dotnet run --no-restore --configuration Release --project LyricHover.Tests
dotnet build --no-restore --configuration Release LyricHover.sln
```

## 项目结构

```text
LyricHover.App/    WPF 桌面应用、歌词岛窗口、歌词坞、设置与原生媒体会话
LyricHover.Core/   歌词匹配、解析、缓存、布局与通用业务逻辑
LyricHover.Tests/  轻量自动化回归测试套件
store/             Microsoft Store 身份、资源与 MSIX 构建脚本
docs/              设计、计划、兼容性矩阵与项目资料
website/           LyricHover 官方网站与相关 API
视觉宣传/          中英文宣传海报
```

## 参与与支持

- 遇到问题时，请在 [Issues](https://github.com/BochengYao/LyricIsland/issues) 中附上 Windows 版本、播放器名称、歌词源和复现步骤。
- 产品建议可以通过[用户激励与反馈页面](https://lyric-island.top/incentives/)提交。
- 大改动建议先开 Issue 讨论，避免实现方向与现有设计冲突。

## 开源许可证

本项目采用 GNU General Public License v3.0 only（GPL-3.0-only）开源许可。详情请参阅 [LICENSE](LICENSE)。

## 说明

LyricHover 是独立项目，与 Apple、腾讯、网易、酷狗、酷我、Spotify 及其音乐服务不存在隶属、合作或背书关系。相关名称与商标归各自权利人所有。歌词内容来自第三方歌词服务，使用时请尊重内容版权与服务条款。

© 2026 LyricHover

---

<a id="english"></a>
# LyricHover (English)

[中文](#lyricHover) · [Download](https://apps.microsoft.com/detail/9NRXZP5HMXK2) · [Website](https://lyric-island.top/) · [Feedback](https://lyric-island.top/incentives/) · [Issues](https://github.com/BochengYao/LyricIsland/issues)

> Every lyric deserves to be seen.

LyricHover is a desktop lyrics tool for Windows 10/11. Lyrics float elegantly at the top of your screen when music plays, and gracefully collapse when playback stops. It connects to your music player through native Windows media sessions, matches synchronized lyrics and translations from multiple sources, and combines lyrics, album art, track info, playback controls and progress into your very own LyricHover.

QQ Music, NetEase Cloud Music, KuGou, KuWo, Spotify, and Apple Music can all be recognized and switched between in the same lightweight, cohesive desktop experience.

![LyricHover hero](视觉宣传/en/1.png)

**[Download from Microsoft Store →](https://apps.microsoft.com/detail/9NRXZP5HMXK2)**

## Key Features

- **Always present, never intrusive.** Lyrics appear at the top edge, expand when needed, and collapse naturally when not playing.
- **Switch players, lyrics stay.** Automatically detects your current music player, or let you pick a preferred one.
- **Expand your way.** Six module types, two layouts, independent compact/expanded configurations — all adjustable on the live island.
- **Mouse nearby, content still shines.** Only the area near the cursor fades, keeping everything else readable and interactive.
- **More reliable lyrics & translations.** Multi-source fallback, smart translation reuse, stable caching and timeline compensation reduce missing lyrics, flickering and desync.
- **Lyric Dock.** Real-time lyrics in the Windows taskbar with left-aligned/centered text, marquee scrolling for long lines, and the same transition animation as the island.
- **Core features always free.** Optionally join LyricHover Pro to support continued development.

## Visual Preview

<table>
  <tr>
    <td width="50%"><img src="视觉宣传/en/2.png" alt="Cursor-aware transparency" /></td>
    <td width="50%"><img src="视觉宣传/en/3.png" alt="Modular LyricHover layouts" /></td>
  </tr>
  <tr>
    <td width="50%"><img src="视觉宣传/en/4.png" alt="Automatic reveal and retract" /></td>
    <td width="50%"><img src="视觉宣传/en/6.png" alt="Multiple music players" /></td>
  </tr>
</table>

## Player Compatibility

| Player | SMTC Support | Notes |
|---|---:|---|
| Apple Music | ✓ | Full support for track info, artwork, playback state, timeline and controls. |
| QQ Music | ✓ | Supports info and controls published to Windows media session. |
| NetEase Cloud Music | ✓ | Playback, switching, lyrics and controls work; seeking in-player won't sync lyrics in real-time. |
| KuGou Music | ✓ | Supports info and controls published to Windows media session. |
| KuWo Music | ✓ | Supports info and controls published to Windows media session. |
| Spotify | ✓ | Supports info and controls published to Windows media session. |

## Running from Source

Requirements: Windows 10/11 x64, Git, .NET SDK for `netcoreapp3.1` WPF projects, Windows 10 SDK `10.0.19041` or compatible.

```powershell
git clone https://github.com/BochengYao/LyricIsland.git
cd LyricIsland
dotnet restore LyricHover.sln
.\run.ps1
```

## Project Structure

```text
LyricHover.App/    WPF desktop app, island window, Lyric Dock, settings & native media sessions
LyricHover.Core/   Lyrics matching, parsing, caching, layout & shared business logic
LyricHover.Tests/  Lightweight automated regression test suite
store/             Microsoft Store identity, assets & MSIX build scripts
docs/              Design, plans, compatibility matrices & project docs
website/           LyricHover official website & related APIs
视觉宣传/          Chinese and English campaign artwork
```

## License

This project is licensed under the GNU General Public License v3.0 only (GPL-3.0-only). See [LICENSE](LICENSE) for details.

## Disclaimer

LyricHover is an independent project and is not affiliated with, partnered with, or endorsed by Apple, Tencent, NetEase, KuGou, KuWo, Spotify, or their music services. Related names and trademarks belong to their respective owners. Lyrics content comes from third-party services; please respect content copyright and terms of service.

© 2026 LyricHover
