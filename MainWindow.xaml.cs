using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace memreduct_winui;

public sealed partial class MainWindow : Window
{
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon("Assets/AppIcon.ico");

        NavView.ItemInvoked += OnNavItemInvoked;
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(MainPage));

        ContentFrame.Navigated += (s, e) =>
        {
            if (e.Content is MainPage mp) mp.ApplyLocalization();
            else if (e.Content is SettingsPage sp) sp.ApplyLocalization();
        };

        TrayIcon.TrayCommand += OnTrayCommand;
        TrayIcon.HotkeyPressed += OnHotkeyPressed;
        Closed += (s, e) => TrayIcon.Destroy();
        AppWindow.Closing += AppWindow_Closing;
        UpdateTrayMenuTexts();
        TrayIcon.RefreshHotkey();
    }

    private void OnTrayCommand(int cmd)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (cmd)
            {
                case TrayIcon.CMD_SHOW:
                    if (AppWindow.IsVisible)
                        AppWindow.Hide();
                    else
                        Activate();
                    break;
                case TrayIcon.CMD_CLEAN:
                    if (!AppWindow.IsVisible)
                        Activate();
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(200);
                        ContentFrame.Navigate(typeof(MainPage));
                        NavView.SelectedItem = NavView.MenuItems[0];
                        if (ContentFrame.Content is MainPage mp) mp.TriggerClean();
                    });
                    break;
                case TrayIcon.CMD_SETTINGS:
                    Activate();
                    NavView.SelectedItem = NavView.MenuItems[1];
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
                case TrayIcon.CMD_EXIT:
                    _exiting = true;
                    TrayIcon.Destroy();
                    Close();
                    break;
            }
        });
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (!_exiting)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void UpdateTrayMenuTexts()
    {
        TrayIcon.SetMenuTexts(
            CoreService.GetString(StrId.TrayShow) ?? "Show / Hide",
            CoreService.GetString(StrId.CleanMemory) ?? "Clean memory",
            CoreService.GetString(StrId.Settings) ?? "Settings",
            "Exit");
    }

    public void RefreshTrayMenu() => UpdateTrayMenuTexts();

    private async void OnHotkeyPressed()
    {
        if (!CoreService.IsElevated()) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            // if on main page, show cleaning animation
            if (ContentFrame.Content is MainPage mp)
                mp.SetCleaningState(true);

            var result = await System.Threading.Tasks.Task.Run(() =>
                CoreService.CleanMemory(IniConfig.ReadUInt("ReductMask2", MemoryMask.Default)));

            if (ContentFrame.Content is MainPage mainPage)
            {
                mainPage.SetCleaningState(false);
                mainPage.ApplyLocalization();
            }

            if (result.Success && result.BytesFreed > 0 && IniConfig.ReadBool("BalloonCleanResults", true))
                ToastService.ShowCleanResult(result.BytesFreed, result.FreedFormatted);
        });
    }

    private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.InvokedItemContainer?.Tag?.ToString();
        if (tag == "main")
        {
            ContentFrame.Navigate(typeof(MainPage));
        }
        else if (tag == "settings")
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
    }
}
