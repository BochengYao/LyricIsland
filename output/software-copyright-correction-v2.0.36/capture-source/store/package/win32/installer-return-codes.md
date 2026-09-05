# Lyric Island Installer Return Codes

This page documents the return codes for `LyricIslandSetup.exe`.

## Install

Command:

```text
LyricIslandSetup.exe /silent
```

Return codes:

| Code | Meaning |
|---:|---|
| 0 | Installation succeeded. |
| 1 | Installation failed due to an unexpected installer error. |

## Uninstall

Command:

```text
LyricIslandSetup.exe /uninstall /silent
```

Return codes:

| Code | Meaning |
|---:|---|
| 0 | Uninstallation succeeded. |
| 1 | Uninstallation failed due to an unexpected installer error. |

## Notes

- The installer installs per user under `%LOCALAPPDATA%\Programs\LyricIsland`.
- The installer creates a Start Menu shortcut named `Lyric Island`.
- The installer writes uninstall metadata under the current user's uninstall registry key.
- The installer does not require administrator privileges.
