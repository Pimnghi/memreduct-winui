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
.\build-publish.ps1

# Build one architecture only
.\build-publish.ps1 -Platform x64
```

Outputs: `artifacts\publish\win-x64\` and
`artifacts\publish\win-arm64\`.

The script performs a clean native build, verifies version consistency, and
checks that each published `CoreLib.dll` and `mrw-cli.exe` matches its target
architecture.

To build the upload-ready versioned ZIP archives and checksums:

```powershell
.\build-portable.ps1
```

Portable release files are written to `artifacts\portable\`.

To build the self-contained per-machine installers for x64 and ARM64:

```powershell
.\build-installer.ps1
```

Inno Setup 7.0.2 or later is required. Installers and their checksum files are
written to `artifacts\installer\`. The application is installed under
`Program Files\Mem Reduct WinUI`; upgrades preserve settings, and uninstall
offers to remove settings and logs.

## Command Line

```powershell
mrw-cli.exe
mrw-cli.exe -clean
mrw-cli.exe -clean:full
```

Run `mrw-cli.exe` without arguments to display detailed help in the language
selected by the application. `-h`, `--help`, and `/?` display the same help
without requesting elevation.
`-clean` uses the saved cleanup mask and `-clean:full` selects every region.
Exit code `0` means full success, `1` means failure or partial failure, and `2`
means invalid arguments or elevation failure.
`mrw-cli.exe` waits in the current terminal while the elevated worker runs and
returns its localized result without opening another Command Prompt window.
The original command-line parameters on `memreduct-winui.exe` remain available
for compatibility.

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
├── CliHost/                   Native console entry point (mrw-cli.exe)
├── language/
│   └── memreduct-winui.lng    Language translations
├── Assets/                    App icons and images
├── app.manifest               Win32 app manifest
└── memreduct-winui.csproj     Project file
```

## Configuration

The portable build stores settings in `data\memreduct-winui.ini` alongside the
executable. The installed build stores settings in
`%ProgramData%\Mem Reduct WinUI\data\memreduct-winui.ini`. When cleanup
logging is enabled, results are written to the corresponding `data` directory.

## License

This project is a refactoring based on Henry++'s
[Mem Reduct](https://github.com/henrypp/memreduct) and is distributed under the
[GNU General Public License v3.0](LICENSE).
