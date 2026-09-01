using System;
using System.IO;

namespace MemReduct.WinUI.Core;

internal static class InstallContext
{
    private const string InstalledModeMarker = "installed.marker";
    private const string ProductDirectory = "Mem Reduct WinUI";

    internal static bool IsInstalled =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, InstalledModeMarker));

    internal static string DataDirectory => IsInstalled
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ProductDirectory,
            "data")
        : Path.Combine(AppContext.BaseDirectory, "data");
}
