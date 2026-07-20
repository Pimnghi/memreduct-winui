using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

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
}
