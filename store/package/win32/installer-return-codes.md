# LyricHover Installer Return Codes

This page is retained only as a historical return-code reference. LyricHover no
longer publishes or supports a standalone Win32 installer; use the Microsoft
Store package instead.

## Install

The historical installer accepted `/silent`.

Return codes:

| Code | Meaning |
|---:|---|
| 0 | Installation succeeded. |
| 1 | Installation failed due to an unexpected installer error. |

## Uninstall

The historical installer accepted `/uninstall /silent`.

Return codes:

| Code | Meaning |
|---:|---|
| 0 | Uninstallation succeeded. |
| 1 | Uninstallation failed due to an unexpected installer error. |

## Notes

- The archived installer is not part of any LyricHover release.
- Current releases display as `LyricHover` and use the Microsoft Store update
  path.
