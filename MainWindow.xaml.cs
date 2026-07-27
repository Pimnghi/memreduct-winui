using MemReduct.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace memreduct_winui;

public sealed partial class MainWindow : Window
{
    private bool _exiting;
    private TrayMenuWindow? _trayMenuWindow;

    private static readonly nint HWND_TOPMOST = -1;
    private static readonly nint HWND_NOTOPMOST = -2;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const double CaptionButtonBottomInset = 1.0;

    [DllImport("user32")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32")]
    private static extern nint MonitorFromRect(ref RECT lprc, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBar.Loaded += OnAppTitleBarLoaded;

        UpdateTitleBarColors();
        Activated += OnWindowActivated;

        if (Content is FrameworkElement fe)
            fe.ActualThemeChanged += (s, e) => UpdateTitleBarColors();

        NavView.ItemInvoked += OnNavItemInvoked;
        NavView.SelectedItem = NavView.FooterMenuItems[0];
        ContentFrame.Navigate(typeof(MainPage));

        ContentFrame.Navigated += (s, e) =>
        {
            if (e.Content is MainPage mp) mp.ApplyLocalization();
            else if (e.Content is SettingsPage sp) sp.ApplyLocalization();
        };

        TrayIcon.TrayCommand += OnTrayCommand;
        TrayIcon.HotkeyPressed += OnHotkeyPressed;
        TrayIcon.TrayClickAction += OnTrayClickAction;
        TrayIcon.ContextMenuRequested += OnTrayContextMenuRequested;
        Closed += (s, e) =>
        {
            _trayMenuWindow?.Close();
            TrayIcon.Destroy();
        };
        AppWindow.Closing += AppWindow_Closing;
        UpdateTrayMenuTexts();
        if (!TrayIcon.RefreshHotkey())
            IniConfig.WriteBool("HotkeyCleanEnable", false);
        ApplyTopmost();
        ApplyNavLocalization();
        RestoreWindowBounds();

        AppWindow.Changed += (s, e) =>
        {
            if (e.DidPositionChange || e.DidSizeChange)
                SaveWindowBounds();
        };
    }

    // 注意：已删除多余的 OnHamburgerClick 方法，NavigationView 内置汉堡按钮会自动处理点击展开/折叠推移动画

    private void UpdateTitleBarColors()
    {
        var theme = Content is FrameworkElement fe ? fe.ActualTheme : ElementTheme.Default;
        TrayIcon.SetMenuTheme(theme == ElementTheme.Dark);

        if (!AppWindowTitleBar.IsCustomizationSupported())
            return;

        var tb = AppWindow.TitleBar;
        tb.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        tb.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        if (theme == ElementTheme.Dark)
        {
            tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 220, 220, 220);
            tb.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 240, 240, 240);
            tb.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 240, 240, 240);
            tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
            tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(24, 255, 255, 255);
            tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(14, 255, 255, 255);
        }
        else
        {
            tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 40, 40, 40);
            tb.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);
            tb.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);
            tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
            tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(16, 0, 0, 0);
            tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(10, 0, 0, 0);
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        var opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.55 : 1.0;
        TitleBarIcon.Opacity = opacity;
        TitleLabel.Opacity = opacity;
    }

    private void OnAppTitleBarLoaded(object sender, RoutedEventArgs args)
    {
        SyncTitleBarHeight();
        if (AppTitleBar.XamlRoot != null)
            AppTitleBar.XamlRoot.Changed += OnTitleBarXamlRootChanged;
    }

    private void OnTitleBarXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(SyncTitleBarHeight);
    }

    private void SyncTitleBarHeight()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported() || AppTitleBar.XamlRoot == null)
            return;

        var scale = AppTitleBar.XamlRoot.RasterizationScale;
        var systemHeight = AppWindow.TitleBar.Height;
        if (scale <= 0 || systemHeight <= 0)
            return;

        var height = Math.Max(0, systemHeight / scale - CaptionButtonBottomInset);
        if (Math.Abs(TitleBarRow.Height.Value - height) > 0.01)
            TitleBarRow.Height = new GridLength(height);
    }

    public void ApplyTopmost()
    {
        SetAlwaysOnTop(IniConfig.ReadBool("AlwaysOnTop"));
    }

    public void SetAlwaysOnTop(bool topmost)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == nint.Zero) return;
        SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    private void RestoreWindowBounds()
    {
        var w = IniConfig.ReadInt("WindowWidth", 0);
        var h = IniConfig.ReadInt("WindowHeight", 0);
        var x = IniConfig.ReadInt("WindowLeft", int.MinValue);
        var y = IniConfig.ReadInt("WindowTop", int.MinValue);

        if (w > 0 && h > 0 && x != int.MinValue && y != int.MinValue
            && IsWindowRectVisible(x, y, w, h))
        {
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h));
        }
        else if (w > 0 && h > 0)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }
        else
        {
            var screenW = GetSystemMetrics(0);
            var screenH = GetSystemMetrics(1);
            var width = Math.Min(1600, screenW);
            var height = Math.Min(1000, screenH);
            var cx = Math.Max(0, (screenW - width) / 2);
            var cy = Math.Max(0, (screenH - height) / 2);
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(cx, cy, width, height));
        }
    }

    private static bool IsWindowRectVisible(int x, int y, int width, int height)
    {
        var rect = new RECT
        {
            Left = x,
            Top = y,
            Right = x + width,
            Bottom = y + height
        };
        return MonitorFromRect(ref rect, 0) != nint.Zero;
    }

    private void SaveWindowBounds()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter
            && presenter.State != OverlappedPresenterState.Restored)
        {
            return;
        }

        var pos = AppWindow.Position;
        var size = AppWindow.Size;
        IniConfig.WriteInt("WindowLeft", pos.X);
        IniConfig.WriteInt("WindowTop", pos.Y);
        IniConfig.WriteInt("WindowWidth", size.Width);
        IniConfig.WriteInt("WindowHeight", size.Height);
    }

    private void OnTrayCommand(int cmd)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (cmd)
            {
                case TrayIcon.CMD_SHOW:
                    if (AppWindow.IsVisible) AppWindow.Hide(); else Activate();
                    break;
                case TrayIcon.CMD_CLEAN:
                    if (!AppWindow.IsVisible) Activate();
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(200);
                        ContentFrame.Navigate(typeof(MainPage));
                        NavView.SelectedItem = NavView.FooterMenuItems[0];
                        if (ContentFrame.Content is MainPage mp) mp.TriggerClean();
                    });
                    break;
                case TrayIcon.CMD_SETTINGS:
                    Activate();
                    NavView.SelectedItem = NavView.FooterMenuItems[1];
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
        if (!_exiting) { args.Cancel = true; sender.Hide(); }
    }

    private void UpdateTrayMenuTexts()
    {
        TrayIcon.SetMenuTexts(
            CoreService.GetString(StrId.TrayShow) ?? "Show / Hide",
            CoreService.GetString(StrId.CleanMemory) ?? "Clean memory",
            CoreService.GetString(StrId.Settings) ?? "Settings",
            CoreService.GetString(StrId.Exit) ?? "Exit");
    }

    public void RefreshTrayMenu() => UpdateTrayMenuTexts();

    public void ApplyLocalization()
    {
        ApplyNavLocalization();
        UpdateTrayMenuTexts();

        if (ContentFrame.Content is MainPage mainPage)
            mainPage.ApplyLocalization();
        else if (ContentFrame.Content is SettingsPage settingsPage)
            settingsPage.ApplyLocalization();
    }

    private void OnTrayContextMenuRequested(int cursorX, int cursorY)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _trayMenuWindow?.Close();

            var menu = new TrayMenuWindow(
                TrayIcon.UseDarkMenu,
                TrayIcon.ShowMenuText,
                TrayIcon.CleanMenuText,
                TrayIcon.SettingsMenuText,
                TrayIcon.ExitMenuText);
            _trayMenuWindow = menu;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_trayMenuWindow, menu))
                    _trayMenuWindow = null;
            };
            menu.ShowAt(cursorX, cursorY);
        });
    }

    public void ApplyNavLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);
        var v = s(StrId.GroupPhysical);       if (v != null) NavMemory.Content = v;
        v = s(StrId.Settings);                if (v != null) NavSettings.Content = v;
        v = s(StrId.About);                   if (v != null) NavAbout.Content = v;
    }

    private void OnHotkeyPressed()
    {
        if (!CoreService.IsElevated()) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (ContentFrame.Content is MainPage mp) mp.SetCleaningState(true);
            var result = await CleanupCoordinator.CleanAsync(CleanupSource.Hotkey);
            if (ContentFrame.Content is MainPage mainPage)
            {
                mainPage.SetCleaningState(false);
                mainPage.ApplyLocalization();
            }
            if (result is { Success: true, BytesFreed: > 0 }
                && IniConfig.ReadBool("BalloonCleanResults", true))
                ToastService.ShowCleanResult(result.BytesFreed, result.FreedFormatted);
        });
    }

    private void OnTrayClickAction(int action)
    {
        switch (action)
        {
            case TrayIcon.ACTION_SHOW:
                if (AppWindow.IsVisible) AppWindow.Hide(); else Activate();
                break;
            case TrayIcon.ACTION_CLEAN:
                OnHotkeyPressed();
                break;
            case TrayIcon.ACTION_TASKMGR:
                System.Diagnostics.Process.Start("taskmgr.exe");
                break;
        }
    }

    private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.InvokedItemContainer?.Tag?.ToString();
        if (tag == "main" && ContentFrame.Content is not MainPage) { ContentFrame.Navigate(typeof(MainPage)); }
        else if (tag == "settings" && ContentFrame.Content is not SettingsPage) { ContentFrame.Navigate(typeof(SettingsPage)); }
        else if (tag == "about" && ContentFrame.Content is not AboutPage) { ContentFrame.Navigate(typeof(AboutPage)); }
    }
}
