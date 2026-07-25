# Mem Reduct WinUI

Real-time memory management application with WinUI 3 native interface. Built on the original [Mem Reduct](https://github.com/henrypp/memreduct) core engine.

## Features

- Real-time monitoring of physical memory, pagefile, and system working set
- One-click memory cleanup using Windows Native API
- System tray with right-click menu and balloon notifications
- Multi-language support (30+ languages)
- Auto-clean based on memory threshold or time interval
- Global hotkey support
- Dark/Light theme following Windows system settings
- Command-line mode (`-clean`, `-clean:full`)
- Single instance with tray minimization
- Startup with Windows (scheduled task, no UAC prompt)

## System Requirements

- Windows 10 version 17763 or later (64-bit or ARM64)
- Administrator privileges (auto-elevation on launch)

## Build

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 with C++ desktop development workload

### Build Steps

```powershell
# Build and publish both portable architectures
.\build_winui.ps1

# Build one architecture only
.\build_winui.ps1 -Platform x64
```

Outputs: `artifacts\win-x64\` and `artifacts\win-arm64\`.

The script performs a clean native build, verifies version consistency, and
checks that each published `CoreLib.dll` matches its target architecture.

## Command Line

```powershell
memreduct-winui.exe -clean
memreduct-winui.exe -clean:full
```

`-clean` uses the saved cleanup mask and `-clean:full` selects every region.
Exit code `0` means full success, `1` means failure or partial failure, and `2`
means invalid arguments or elevation failure.

## Project Structure

```
memreduct-winui/
├── App.xaml(.cs)              Application entry
├── MainWindow.xaml(.cs)       Main window, navigation, tray, hotkey
├── MainPage.xaml(.cs)         Memory statistics + clean
├── SettingsPage.xaml(.cs)     Settings
├── AboutPage.xaml(.cs)        About dialog
├── Core/                      C# bridge layer
│   ├── CoreService.cs         C DLL wrappers
│   ├── NativeMethods.cs       P/Invoke declarations
│   ├── IniConfig.cs           INI config via kernel32
│   ├── TrayIcon.cs            SysTray icon + context menu
│   ├── AutoCleanService.cs    Background auto-clean timer
│   ├── ToastService.cs        Balloon notification helper
│   └── StrId.cs               String resource ID constants
├── CoreLib/                   Native C DLL
│   ├── core.h / core.c        Memory cleanup core
│   ├── CoreLib.def            Exported functions
│   ├── CoreLib.vcxproj        MSBuild project
│   └── routine/               Shared C library
├── language/
│   └── memreduct-winui.lng    Language translations
├── Assets/                    App icons and images
├── app.manifest               Win32 app manifest
└── memreduct-winui.csproj     Project file
```

## Configuration

All settings are stored in `data\memreduct-winui.ini` alongside the executable
(portable mode). When cleanup logging is enabled, results are written to
`data\memreduct-winui.log`.

## License

Based on [Mem Reduct](https://github.com/henrypp/memreduct) by Henry++. MIT License.
