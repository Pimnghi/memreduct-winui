using MemReduct.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
