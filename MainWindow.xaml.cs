using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace memreduct_winui;

public sealed partial class MainWindow : Window
{
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

        TrayIcon.TrayRightClick += OnTrayRightClick;
        Closed += (s, e) => TrayIcon.Destroy();
    }

    [DllImport("user32")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private void OnTrayRightClick()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var menu = new MenuFlyout();

            var showItem = new MenuFlyoutItem
            {
                Text = CoreService.GetString(StrId.GroupPhysical) ?? "Show",
                Icon = new SymbolIcon(Symbol.Home),
            };
            showItem.Click += (s, e) =>
            {
                this.Activate();
            };
            menu.Items.Add(showItem);

            var cleanItem = new MenuFlyoutItem
            {
                Text = CoreService.GetString(StrId.CleanMemory) ?? "Clean memory",
                Icon = new SymbolIcon(Symbol.Refresh),
            };
            cleanItem.Click += (s, e) =>
            {
                if (ContentFrame.Content is MainPage mp)
                    mp.TriggerClean();
            };
            menu.Items.Add(cleanItem);

            menu.Items.Add(new MenuFlyoutSeparator());

            var settingsItem = new MenuFlyoutItem
            {
                Text = CoreService.GetString(StrId.SettingsGeneral) ?? "Settings",
                Icon = new SymbolIcon(Symbol.Setting),
            };
            settingsItem.Click += (s, e) => NavView.SelectedItem = NavView.MenuItems[1];
            menu.Items.Add(settingsItem);

            var exitItem = new MenuFlyoutItem
            {
                Text = "Exit",
                Icon = new SymbolIcon(Symbol.Cancel),
            };
            exitItem.Click += (s, e) => Close();
            menu.Items.Add(exitItem);

            // show flyout at cursor position
            if (GetCursorPos(out var pt))
            {
                menu.ShowAt(Content, new Windows.Foundation.Point(pt.X, pt.Y));
            }
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
