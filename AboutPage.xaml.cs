using Microsoft.UI.Xaml.Controls;

namespace memreduct_winui;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        if (version != null)
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
    }
}
