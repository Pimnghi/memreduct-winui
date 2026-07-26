using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace memreduct_winui;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        if (version != null)
            VersionText.Text = $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private async void OnWebsiteClick(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/pimnghi/memreduct-winui"));
    }
}
