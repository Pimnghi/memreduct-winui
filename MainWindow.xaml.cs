using MemReduct.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace memreduct_winui;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueueTimer _timer;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;
        _timer.Start();

        UpdateDisplay();
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var stats = CoreService.GetMemoryStats();

        PhysicalBar.Value = stats.PhysicalPercent;
        PhysicalFreeText.Text = FormatBytes(stats.PhysicalFree);
        PhysicalTotalText.Text = FormatBytes(stats.PhysicalTotal);

        PageFileBar.Value = stats.PageFilePercent;
        PageFileFreeText.Text = FormatBytes(stats.PageFileFree);
        PageFileTotalText.Text = FormatBytes(stats.PageFileTotal);

        CacheBar.Value = stats.SystemCachePercent;
        CacheFreeText.Text = FormatBytes(stats.SystemCacheFree);
        CacheTotalText.Text = FormatBytes(stats.SystemCacheTotal);
    }

    private static string FormatBytes(ulong bytes)
    {
        return bytes switch
        {
            >= 1073741824 => $"{bytes / 1073741824.0:F2} GB",
            >= 1048576 => $"{bytes / 1048576.0:F1} MB",
            >= 1024 => $"{bytes / 1024.0:F0} KB",
            _ => $"{bytes} B"
        };
    }

    private async void OnCleanClick(object sender, RoutedEventArgs e)
    {
        if (!CoreService.IsElevated())
        {
            ResultBar.Title = "Administrator privileges required";
            ResultBar.Message = "Please run the program as administrator.";
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.IsOpen = true;
            return;
        }

        CleanBtn.IsEnabled = false;
        CleanBtn.Content = "Cleaning...";

        var result = await System.Threading.Tasks.Task.Run(() =>
            CoreService.CleanMemory(MemoryMask.Default));

        CleanBtn.IsEnabled = true;
        CleanBtn.Content = "Clean memory";

        if (result.Success && result.BytesFreed > 0)
        {
            ResultBar.Title = "Memory cleaned";
            ResultBar.Message = $"Released: {result.FreedFormatted}";
            ResultBar.Severity = InfoBarSeverity.Success;
        }
        else
        {
            ResultBar.Title = "Memory cleaned";
            ResultBar.Message = "No significant memory was released.";
            ResultBar.Severity = InfoBarSeverity.Informational;
        }
        ResultBar.IsOpen = true;
    }
}
