# Apple Music Desktop Lyrics

A Windows desktop lyrics companion for Apple Music. It reads the current Windows media session, searches synced lyrics from multiple sources, and displays them as a top-edge dynamic-island style overlay.

## Features

- Top-edge lyrics island that retracts off-screen when playback stops.
- Synced lyric lookup with fallback sources: LRCLIB, QQ Music, KuGou, and NetEase.
- Optional Chinese translations when the lyrics source provides them.
- Single-line and multi-line lyric display modes.
- Long lyric marquee animation instead of truncating with ellipses.
- User settings for display, monitor position, cache size, and mouse avoidance.
- Mouse-proximity transparency with configurable detection range, aura size, aspect ratio, and transparency spectrum.
- Local LRU lyric cache under `%LOCALAPPDATA%\AppleMusicDesktopLyrics\lyrics`.
- Single-instance guard so repeated exe launches reuse one running instance.

## Run

Open PowerShell:

```powershell
cd D:\AppleMusicDesktopLyrics
.\run.ps1
```

Then play a song in Apple Music.

## Published Build

The local published executable is generated under:

```text
D:\AppleMusicDesktopLyrics\publish\AppleMusicDesktopLyrics.App.exe
```

The `publish/`, `bin/`, and `obj/` directories are intentionally ignored by GitHub. Rebuild them locally when needed:

```powershell
dotnet publish D:\AppleMusicDesktopLyrics\AppleMusicDesktopLyrics.App\AppleMusicDesktopLyrics.App.csproj -c Release -o D:\AppleMusicDesktopLyrics\publish
```

## Controls

After clicking the island:

- Right or Up: make lyrics 200 ms earlier.
- Left or Down: make lyrics 200 ms later.
- R: reset lyric offset to the default 800 ms early.
- Right-click: open preferences.

## Verify

```powershell
dotnet run --project D:\AppleMusicDesktopLyrics\AppleMusicDesktopLyrics.Tests\AppleMusicDesktopLyrics.Tests.csproj --no-restore
dotnet build D:\AppleMusicDesktopLyrics\AppleMusicDesktopLyrics.sln --no-restore
```

## Notes

The now-playing reader depends on Windows' system media session service. It is meant to run in a normal desktop session where Apple Music publishes playback metadata through Windows media controls.
