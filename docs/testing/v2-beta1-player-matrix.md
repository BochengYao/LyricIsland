# Lyric Island v2.0 Beta 1 Player Matrix

This matrix records observed behavior only. Empty IDs or `Not verified` entries mean the player was not available or not exercised in the current validation pass.

| Player | SourceAppUserModelId | Metadata | Artwork | Timeline | Previous | Play/Pause | Next | Seek resync | Notes |
|---|---|---|---|---|---|---|---|---|---|
| QQ 音乐 | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Requires live QQ Music desktop session. |
| 网易云音乐 UWP | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Requires live NetEase UWP session. |
| 酷狗音乐 | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Requires live KuGou session. |
| Spotify | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Requires live Spotify session. |
| 酷我音乐 | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Requires live KuWo session. |
| Apple Music regression | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Not verified | Requires live Apple Music playback and lyrics cache check. |

## Layout validation checklist

- A and C layouts retain independent module lists after restart: Not verified live.
- C expands after 180 ms hover and collapses 900 ms after leave: Covered by automated controller tests; live visual pass still pending.
- Pause recovery from top sensor after five seconds: Not verified live.
- Drag each module type from settings to the real island: Not verified live.
- 18 px snapping, midpoint reorder, repeated dividers, cancel rollback, save persistence, close-as-cancel: Core snapping/reorder/rollback covered by automated tests; live visual pass still pending.
- DPI passes at 100%, 125%, 150%, and 200%: Not verified live.

## Automated validation

Use:

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet run --no-restore --project LyricsIsland.Tests
dotnet build LyricsIsland.sln -c Release --no-restore
git diff --check
```
