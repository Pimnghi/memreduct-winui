using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MemReduct.WinUI.Core;
using System;

namespace MemReduct.WinUI.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        if (version != null)
            VersionText.Text = $"{version.Major}.{version.Minor}.{version.Build}";
        ApplyLocalization();
    }

    public void ApplyLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);

        VersionCard.Header = s(StrId.Version) ?? "Version";
        AboutCard.Header = s(StrId.About) ?? "About";
        AboutDescriptionText.Text = s(StrId.AboutDescription)
            ?? "A lightweight real-time memory management application that uses the Windows Native API to clear system caches. Compatible with Windows 10/11 x64 and ARM64.";
        AuthorCard.Header = s(StrId.Author) ?? "Author";
        UpstreamCard.Header = s(StrId.UpstreamProject) ?? "Upstream project";
        ProjectLinkCard.Header = s(StrId.ProjectRepository) ?? "Project repository";
    }

    private async void OnAuthorClick(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/Pimnghi"));
    }

    private async void OnUpstreamClick(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/henrypp/memreduct"));
    }

    private async void OnProjectRepositoryClick(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/Pimnghi/memreduct-winui"));
    }
}
