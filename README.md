# Lyric Island / 歌词岛

**中文** | 给 Windows 版 Apple Music 使用的桌面歌词伴侣。它读取 Windows 当前媒体会话，从多个歌词源查找同步歌词和歌词库翻译，并在屏幕上边缘显示一个可自动弹出、自动收起的“歌词岛”。

**English** | A Windows desktop lyrics companion for Apple Music. It reads the active Windows media session, searches synced lyrics and source-provided translations from multiple providers, and displays them in a top-edge dynamic-island style overlay.

## 功能 / Features

- **顶部歌词岛 / Top-edge lyric island**

  播放时从屏幕上边缘滑出，暂停或停止后收回屏幕外。

  Slides in from the top edge while music is playing, then retracts off-screen when playback pauses or stops.

- **多歌词源 / Multiple lyric sources**

  支持 LRCLIB、QQ 音乐、酷狗、网易云等来源，并可设置首选歌词源；首选源不匹配时会临时自动尝试其他来源。

  Supports LRCLIB, QQ Music, KuGou, NetEase, and fallback matching. You can set a preferred provider, while the app still tries other sources when the preferred one does not fit the current song.

- **同步歌词与翻译 / Synced lyrics and translations**

  优先使用歌词库里已有的同步歌词和中文翻译，不做机器翻译。

  Uses source-provided synced lyrics and Chinese translations when available. It does not generate machine translations by itself.

- **单行/多行显示 / Single-line and multi-line display**

  支持单行歌词、多行歌词；开启翻译时自动使用多行显示。

  Supports single-line and multi-line lyric layouts. Translation mode forces the multi-line layout.

- **长歌词滚动 / Long-line marquee**

  长句不会用省略号截断，会根据歌词时长横向滚动显示完整内容。

  Long lyric lines are not truncated with ellipses; they scroll horizontally across the lyric duration.

- **鼠标避让 / Mouse-aware transparency**

  鼠标靠近歌词岛时，仅鼠标附近区域会降低背景和文字透明度，方便点击或查看下方内容；可调探测范围、光晕大小、长宽比和透明度频谱。

  When the cursor approaches the island, only the nearby area becomes more transparent so the content underneath remains readable and clickable. Detection range, aura size, aspect ratio, and opacity spectrum are configurable.

- **偏好设置 / Preferences**

  设置窗口支持浅色、深色、跟随系统模式，可调整歌词显示、屏幕位置、缓存容量和鼠标避让效果。

  The preferences window supports light, dark, and system theme modes, with controls for lyric display, monitor placement, cache size, and mouse avoidance.

- **本地缓存 / Local cache**

  按歌曲维度维护 LRU 歌词缓存，缓存目录位于 `%LOCALAPPDATA%\AppleMusicDesktopLyrics\lyrics`。

  Maintains a song-level LRU lyric cache under `%LOCALAPPDATA%\AppleMusicDesktopLyrics\lyrics`.

- **单实例运行 / Single-instance guard**

  连续点击 exe 也只会保留一个正在运行的实例。

  Repeated exe launches reuse the existing running instance instead of opening duplicates.

## 运行 / Run

打开 PowerShell 并运行：

Open PowerShell and run:

```powershell
cd D:\AppleMusicDesktopLyrics
.\run.ps1
```

然后在 Apple Music 里播放歌曲。

Then play a song in Apple Music.

## 发布版本 / Published Build

本地发布后的可执行文件位于：

The local published executable is generated under:

```text
D:\AppleMusicDesktopLyrics\publish\AppleMusicDesktopLyrics.App.exe
```

`publish/`、`bin/`、`obj/` 目录不会上传到 GitHub，需要时在本地重新发布：

The `publish/`, `bin/`, and `obj/` directories are intentionally ignored by GitHub. Rebuild them locally when needed:

```powershell
dotnet publish D:\AppleMusicDesktopLyrics\AppleMusicDesktopLyrics.App\AppleMusicDesktopLyrics.App.csproj -c Release -o D:\AppleMusicDesktopLyrics\publish
```

## 控制 / Controls

歌词岛获得焦点后：

After focusing the island:

- `Right` 或 `Up`：歌词提前 200 ms。

  `Right` or `Up`: make lyrics 200 ms earlier.
- `Left` 或 `Down`：歌词延后 200 ms。

  `Left` or `Down`: make lyrics 200 ms later.
- `R`：重置歌词偏移，回到默认提前 800 ms。

  `R`: reset lyric offset to the default 800 ms early.
- 右键歌词岛：打开偏好设置。

  Right-click the island: open preferences.

## 验证 / Verify

```powershell
dotnet run --project D:\AppleMusicDesktopLyrics\AppleMusicDesktopLyrics.Tests\AppleMusicDesktopLyrics.Tests.csproj --no-restore
dotnet build D:\AppleMusicDesktopLyrics\AppleMusicDesktopLyrics.sln --no-restore
```

## 说明 / Notes

当前播放信息依赖 Windows 系统媒体会话服务。请在正常桌面会话中运行，并确保 Apple Music 能通过 Windows 媒体控制发布歌曲信息。

The now-playing reader depends on Windows' system media session service. Run it in a normal desktop session where Apple Music publishes playback metadata through Windows media controls.
