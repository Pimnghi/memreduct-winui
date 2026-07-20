using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace memreduct_winui;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    [DllImport("kernel32")]
    private static extern bool AllocConsole();

    [DllImport("kernel32")]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // command-line mode
        var cmdline = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
        if (cmdline.Contains("-clean") || cmdline.Contains("/clean"))
        {
            var fullMask = cmdline.Contains("full") ? MemReduct.Core.MemoryMask.All : MemReduct.Core.MemoryMask.Default;

#if !DEBUG
            if (!MemReduct.Core.CoreService.IsElevated())
            {
                RunAsAdmin(cmdline);
                return;
            }
#endif

            AttachConsole(ATTACH_PARENT_PROCESS);
            var result = MemReduct.Core.CoreService.CleanMemory(fullMask);
            if (result.Success)
                Console.WriteLine($"Memory released: {result.FreedFormatted}");
            else
                Console.WriteLine("Clean failed.");
            Environment.Exit(result.Success ? 0 : 1);
            return;
        }

#if !DEBUG
        if (!MemReduct.Core.CoreService.IsElevated())
        {
            RunAsAdmin(string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
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

        MemReduct.Core.TrayIcon.Create("Mem Reduct");

        MainWindow = new MainWindow();
        MainWindow.Activate();

        ApplySavedTheme();

        MemReduct.Core.AutoCleanService.Refresh();

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

    private static void RunAsAdmin(string arguments)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(exePath, arguments)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                });
            }
            catch { }
        }
        Environment.Exit(0);
    }
}
