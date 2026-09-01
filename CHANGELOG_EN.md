# Changelog

<p align="center">
  <a href="CHANGELOG.md">简体中文</a> ·
  <strong>English</strong>
</p>

This document records the official releases and notable changes of Mem Reduct
WinUI.

## Unreleased

There are currently no unreleased changes.

## [1.2.0] - 2026-09-01

[GitHub Release](https://github.com/Pimnghi/memreduct-winui/releases/tag/v1.2.0) ·
[Full changelog](https://github.com/Pimnghi/memreduct-winui/compare/v1.1.0...v1.2.0)

This release improves native WinUI interaction and visual consistency in Settings,
and fixes tray-menu and page-layout issues.

### Improved

- Placed native cleanup-region checkboxes to the left of their labels while
  retaining the safeguards for dangerous cleanup regions.
- Made the entire hotkey keycap group clickable to open the editor, removed the
  separate pencil icon, and enlarged the hover and click area.
- Added localized On/Off labels to the left of settings switches with native spacing.
- Organized source code, build scripts, and installer resources under a consistent
  `MemReduct.WinUI` module naming scheme. Public executable names and configuration
  paths remain unchanged.

### Fixed

- Stabilized the tray context menu's background appearance and item heights.
- Restored the title-bar and About-page application icons after the directory reorganization.
- Removed the fixed top gap when scrolling Settings and About while preserving
  the initial page spacing.

## [1.1.0] - 2026-08-18

[GitHub Release](https://github.com/Pimnghi/memreduct-winui/releases/tag/v1.1.0)

This release improves tray-based memory monitoring and the standalone build and
release workflow.

The tray memory usage feature was requested in
[Issue #1](https://github.com/Pimnghi/memreduct-winui/issues/1). Thanks to
@kokpk for the suggestion.

### Added

- Added an optional numeric tray icon that shows physical-memory usage as an
  integer and follows the configured warning and danger colors.
- The tray tooltip now shows precise usage for physical memory, pagefile, and
  the system working set.

### Improved

- Refined numeric tray-icon rendering to remove dark edge artifacts after Shell
  scaling.
- Replaced the tray-usage setting icon with a Windows 10/11-compatible usage
  gauge.
- Improved standalone repository builds and x64/ARM64 GitHub Actions validation.
- Preserved older local packages by storing portable and installer artifacts in
  version-specific directories.
- Improved the bilingual project documentation and dashboard screenshot.

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
