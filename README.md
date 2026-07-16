# Lyric Island / 歌词岛

[English](README_EN.md) · [官方网站](https://lyric-island.top) · [下载版本](https://github.com/BochengYao/LyricIsland/releases) · [问题反馈](https://github.com/BochengYao/LyricIsland/issues)

> 让歌词像系统界面的一部分一样，安静地停留在屏幕顶部。

Lyric Island（歌词岛）是一款面向 Windows 的桌面歌词伴侣。它通过 Windows 系统媒体会话读取当前歌曲，从多个歌词源匹配同步歌词和歌词库翻译，并用一个会随播放状态弹出、展开与收起的顶部“歌词岛”呈现内容。

项目不再以某一个音乐客户端为中心：只要播放器能够正确发布 Windows SMTC 媒体会话，歌词岛就可以尝试读取它的歌曲信息和播放状态。

> **项目状态：** v2.0 Beta 正在持续开发。仓库中的开发分支可能领先于 GitHub Releases；播放器兼容性和部分交互仍在进行实机验证。

## 预览

![歌词岛主视觉](docs/images/poster-hero.png)

![工作场景中的歌词岛](docs/images/poster-workspace.png)

## 核心能力

### 顶部动态歌词岛

- 播放时从屏幕顶部滑出，暂停或停止后收回屏幕外。
- 支持常驻、自动隐藏、悬停展开等交互状态。
- 可选择显示器、停靠边缘和水平位置，适配多显示器桌面。

### 模块化布局

v2.0 Beta 将歌词岛拆分为可组合模块：

- 专辑封面
- 歌词
- 歌曲信息
- 播放控制
- 播放进度
- 分割线

设置窗口提供模块工具箱，可以直接把模块拖到真实歌词岛上进行吸附、重排、保存或取消。紧凑态与展开态布局可以独立保存。

### 多播放器媒体会话

歌词岛通过 Windows SMTC 读取当前媒体会话，可自动跟随最近活跃的播放器，也可以锁定指定播放器。目标播放器包括：

- Apple Music
- QQ 音乐
- 网易云音乐
- 酷狗音乐
- 酷我音乐
- Spotify
- 其他兼容 Windows SMTC 的播放器

不同播放器对封面、时间轴、切歌和进度跳转的支持程度不同。仓库只把已经验证的结果记入[播放器测试矩阵](docs/testing/v2-beta1-player-matrix.md)，未验证项目不会被描述为已完整支持。

### 多歌词源与同步翻译

- 支持 LRCLIB、QQ 音乐、酷狗、网易云等歌词来源。
- 可以选择首选歌词源；首选源不匹配时会临时尝试其他来源。
- 优先使用歌词库已有的逐行时间轴与中文翻译。
- 不在本地自动生成机器翻译。

### 歌词显示细节

- 单行和多行歌词布局。
- 开启翻译时自动使用多行显示。
- 长歌词按当前歌词持续时间横向滚动，不用省略号截断。
- 可调整全局歌词时间偏移，并通过快捷键即时校准。

### 鼠标避让

当鼠标靠近歌词岛时，只降低鼠标附近区域的背景与文字不透明度，方便查看或点击下方内容。探测范围、光晕大小、长宽比和透明度频谱均可配置。

### 设置、缓存与单实例

- 浅色、深色和跟随系统主题。
- 歌词显示、播放器锁定、模块布局、屏幕位置与鼠标避让设置。
- 按歌曲保存的 LRU 歌词缓存，容量可调。
- 重复启动时复用现有实例，不会打开多个歌词岛。

## 快速开始

### 下载发布版本

前往 [GitHub Releases](https://github.com/BochengYao/LyricIsland/releases) 下载发布包，解压后运行 `LyricIsland.App.exe`。

当前公开 Release 可能落后于 v2.0 Beta 开发分支。如果需要体验最新开发状态，请从源码构建。

### 从源码运行

环境要求：

- Windows 10/11 x64
- Git
- 能够构建 `netcoreapp3.1` WPF 项目的 .NET SDK
- Windows 10 SDK `10.0.19041` 或兼容版本

```powershell
git clone https://github.com/BochengYao/LyricIsland.git
cd LyricIsland
dotnet restore
.\run.ps1
```

启动后，在任一支持 Windows SMTC 的播放器中开始播放歌曲。首次启动会显示引导；右键歌词岛可以打开设置。

### 构建发布目录

```powershell
dotnet publish `
  .\AppleMusicDesktopLyrics.App\AppleMusicDesktopLyrics.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\publish\current
```

发布入口为：

```text
publish\current\LyricIsland.App.exe
```

## 操作与快捷键

歌词岛获得焦点后：

| 操作 | 功能 |
|---|---|
| `Right` / `Up` | 歌词提前 200 ms |
| `Left` / `Down` | 歌词延后 200 ms |
| `R` | 重置为默认歌词偏移 |
| 右键歌词岛 | 打开设置 |

默认全局快捷键：

| 快捷键 | 功能 |
|---|---|
| `Ctrl+Alt+Left` | 歌词偏移减少 500 ms |
| `Ctrl+Alt+Right` | 歌词偏移增加 500 ms |
| `Ctrl+Alt+Down` | 重置歌词偏移 |

快捷键可以在设置中调整；如果与其他软件冲突，请重新绑定。

## 本地数据与隐私

歌词岛在本地保存设置和歌词缓存：

```text
%LOCALAPPDATA%\AppleMusicDesktopLyrics\settings.json
%LOCALAPPDATA%\AppleMusicDesktopLyrics\lyrics\
```

当前仍保留旧目录名，以兼容已安装版本的数据。桌面端不会要求登录歌词岛账号。歌词搜索会向已启用的第三方歌词源发送必要的歌曲标题、歌手等匹配信息；请同时遵守相应服务的条款。

## 项目结构

```text
AppleMusicDesktopLyrics.App/    WPF 桌面应用、歌词岛窗口、设置与媒体会话
AppleMusicDesktopLyrics.Core/   歌词匹配、解析、缓存、布局和通用业务逻辑
AppleMusicDesktopLyrics.Tests/  无额外测试框架的自动化回归入口
docs/                           设计、计划、兼容性矩阵与项目资料
website/                        Lyric Island 官方网站与 API（v2 开发分支）
```

内部文件夹仍保留历史名称，是为了减少迁移风险；面向用户的产品名、程序集和发布文件统一使用 **Lyric Island / 歌词岛**。

## 验证

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'

dotnet run --no-restore --configuration Release --project AppleMusicDesktopLyrics.Tests
dotnet build AppleMusicDesktopLyrics.sln -c Release --no-restore
git diff --check
```

播放器相关功能还需要在真实桌面会话中验证，因为自动测试无法替代实际播放器发布的媒体会话、封面和时间轴。

## 参与项目

- 遇到问题时，请在 [Issues](https://github.com/BochengYao/LyricIsland/issues) 中附上 Windows 版本、播放器名称、歌词源和复现步骤。
- 播放器兼容性反馈请说明封面、时间轴、播放控制和切歌分别是否有效。
- 大改动建议先开 Issue 讨论，避免实现方向与现有 v2 设计冲突。

## 说明

Lyric Island / 歌词岛是独立项目，与 Apple、腾讯、网易、酷狗、酷我、Spotify 及其音乐服务不存在隶属或官方合作关系。相关名称与商标归各自权利人所有。歌词内容来自第三方歌词服务，使用时请尊重内容版权和服务条款。

© 2026 Lyric Island · 歌词岛
