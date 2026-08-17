# Changelog

<p align="center">
  <a href="CHANGELOG.md">简体中文</a> ·
  <strong>English</strong>
</p>

This document records the official releases and notable changes of Mem Reduct
WinUI.

## Unreleased

### Added

- Added an optional numeric tray icon for physical-memory usage while retaining
  the precise percentage in the hover tooltip.

## [1.0.0] - 2026-07-31

[GitHub Release](https://github.com/Pimnghi/memreduct-winui/releases/tag/v1.0.0)

The first stable release, featuring a native WinUI 3 redesign of Mem Reduct
while retaining its Windows Native API memory-cleaning capabilities.

### Highlights

- Real-time physical memory, pagefile, and system working-set statistics.
- Multiple cleanup regions with full-success, partial-success, and failure reporting.
- Manual, tray, global-hotkey, automatic, and command-line cleanup entry points.
- Threshold- and interval-based automatic cleanup with dangerous-region protection.
- Fluent-style tray menus, Windows notifications, single-instance behavior, and scheduled-task startup.
- Dark, light, and system themes with more than 30 interface languages.
- `mrw-cli.exe -clean` and `mrw-cli.exe -clean:full` commands.

### Distribution

- Windows 10 version 1809 or later, and Windows 11.
- x64 and ARM64.
- Self-contained Portable ZIP packages and per-machine installers.
- Portable configuration is stored in the `data` directory beside the executable;
  installed configuration is stored in `%ProgramData%\Mem Reduct WinUI\data`.

### Notes

- Administrator privileges are required.
- The 1.0.0 binaries are not Authenticode-signed, so UAC displays “Unknown publisher.”
- This project is based on Mem Reduct by Henry++ and is distributed under the GPL-3.0 license.
