using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace memreduct_winui;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    [DllImport("kernel32")]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var commandArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var isClean = commandArgs.Any(IsCleanArgument);
        var isFullClean = commandArgs.Any(IsFullCleanArgument);
        var isAutostart = commandArgs.Any(IsAutostartArgument);
        var hasInvalidArguments = commandArgs.Any(arg =>
            !IsCleanArgument(arg) && !IsFullCleanArgument(arg) && !IsAutostartArgument(arg));

        if (hasInvalidArguments || (isClean && isAutostart))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Console.Error.WriteLine("Usage: memreduct-winui.exe [-clean|-clean:full|-autostart]");
            Environment.Exit(2);
            return;
        }

        if (isClean)
        {
#if !DEBUG
            if (!MemReduct.Core.CoreService.IsElevated())
            {
                Environment.Exit(RunAsAdmin(commandArgs, waitForExit: true));
                return;
            }
#endif

            AttachConsole(ATTACH_PARENT_PROCESS);
            var mask = isFullClean
                ? MemReduct.Core.MemoryMask.All
                : MemReduct.Core.IniConfig.ReadUInt("ReductMask2", MemReduct.Core.MemoryMask.Default);
            var result = MemReduct.Core.CleanupCoordinator
                .CleanAsync(MemReduct.Core.CleanupSource.CommandLine, mask)
                .GetAwaiter()
                .GetResult();

            if (result?.Status == MemReduct.Core.CleanupStatus.Success)
            {
                Console.WriteLine($"Memory released: {result.FreedFormatted}");
                Environment.Exit(0);
            }
            else
            {
                Console.Error.WriteLine(result?.Status == MemReduct.Core.CleanupStatus.PartialSuccess
                    ? $"Memory cleaning partially failed (failed mask: 0x{result.FailedMask:X2})."
                    : $"Memory cleaning failed (failed mask: 0x{result?.FailedMask ?? mask:X2}).");
                Environment.Exit(1);
            }
            return;
        }

#if !DEBUG
        if (!MemReduct.Core.CoreService.IsElevated())
        {
            Environment.Exit(RunAsAdmin(commandArgs, waitForExit: false));
            return;
        }
#endif

        var appInstance = AppInstance.FindOrRegisterForKey("memreduct_winui_instance");
        if (!appInstance.IsCurrent)
        {
            appInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs()).AsTask().Wait();
            Environment.Exit(0);
            return;
        }

        MemReduct.Core.ToastService.Initialize();
        MemReduct.Core.TrayIcon.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        MemReduct.Core.TrayIcon.Create("Mem Reduct WinUI");

        MainWindow = new MainWindow();
        MainWindow.Activate();

        ApplySavedTheme();

        if (MemReduct.Core.IniConfig.ReadBool("IsStartMinimized") || isAutostart)
            MainWindow.AppWindow.Hide();

        MemReduct.Core.AutoCleanService.Refresh();
        MemReduct.Core.AutoStartService.EnsureConfigured();

        appInstance.Activated += (s, e) =>
        {
            MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (MainWindow is MainWindow w && w.AppWindow != null)
                    w.AppWindow.Show(true);
            });
        };
    }

    public static void ApplyTheme(string? theme)
    {
        if (theme == null) theme = MemReduct.Core.IniConfig.ReadString("Theme", "System") ?? "System";
        var value = theme == "Dark" ? ElementTheme.Dark :
                    theme == "Light" ? ElementTheme.Light : ElementTheme.Default;

        if (MainWindow?.Content is FrameworkElement fe)
            fe.RequestedTheme = value;
    }

    private static void ApplySavedTheme()
    {
        ApplyTheme(null);
    }

    private static bool IsCleanArgument(string value) =>
        value.Equals("-clean", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/clean", StringComparison.OrdinalIgnoreCase)
        || IsFullCleanArgument(value);

    private static bool IsFullCleanArgument(string value) =>
        value.Equals("-clean:full", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/clean:full", StringComparison.OrdinalIgnoreCase);

    private static bool IsAutostartArgument(string value) =>
        value.Equals("-autostart", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/autostart", StringComparison.OrdinalIgnoreCase);

    private static int RunAsAdmin(string[] arguments, bool waitForExit)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return 2;

        try
        {
            var startInfo = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return 2;

            if (waitForExit)
            {
                process.WaitForExit();
                return process.ExitCode;
            }

            return 0;
        }
        catch
        {
            return 2;
        }
    }
}
