using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;

namespace memreduct_winui;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
#if !DEBUG
        if (!MemReduct.Core.CoreService.IsElevated())
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exePath)
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = Path.GetDirectoryName(exePath),
                    });
                    Environment.Exit(0);
                    return;
                }
                catch { }
            }
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
}
