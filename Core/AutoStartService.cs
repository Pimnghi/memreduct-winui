using System;
using System.Diagnostics;
using System.IO;

namespace MemReduct.Core;

public static class AutoStartService
{
    private const string TaskName = "MemReductWinUI";

    public static void EnsureConfigured()
    {
        if (!IniConfig.ReadBool("LoadOnStartup"))
            return;

        var executablePath = GetExecutablePath();
        if (executablePath == null)
            return;

        var configuredPath = IniConfig.ReadString("AutoStartPath", string.Empty);
        if (!TaskExists() || !string.Equals(configuredPath, executablePath, StringComparison.OrdinalIgnoreCase))
            SetEnabled(true);
    }

    public static bool SetEnabled(bool enabled)
    {
        var executablePath = GetExecutablePath();
        if (executablePath == null)
            return false;

        if (!enabled)
        {
            if (TaskExists() && RunSchtasks("/delete", "/tn", TaskName, "/f") != 0)
                return false;

            IniConfig.WriteBool("LoadOnStartup", false);
            IniConfig.WriteString("AutoStartPath", string.Empty);
            return true;
        }

        var taskCommand = $"\"{executablePath}\" -autostart";
        var exitCode = RunSchtasks(
            "/create",
            "/tn", TaskName,
            "/tr", taskCommand,
            "/sc", "onlogon",
            "/rl", "highest",
            "/f");

        if (exitCode != 0)
            return false;

        IniConfig.WriteBool("LoadOnStartup", true);
        IniConfig.WriteString("AutoStartPath", executablePath);
        return true;
    }

    private static bool TaskExists() =>
        RunSchtasks("/query", "/tn", TaskName) == 0;

    private static int RunSchtasks(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return -1;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static string? GetExecutablePath()
    {
        var path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFullPath(path) : null;
    }
}
