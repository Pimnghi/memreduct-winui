using MemReduct.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace memreduct_winui;

public sealed partial class MainPage : Page
{
    private DispatcherQueueTimer? _timer;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _timer ??= DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_timer == null) return;

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;
        _timer.Start();

        ApplyLocalization();
        UpdateDisplay();
    }

    protected override void OnNavigatingFrom(Microsoft.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        _timer?.Stop();
        if (_timer != null) _timer.Tick -= OnTimerTick;
    }

    public void TriggerClean()
    {
        OnCleanClick(this, new RoutedEventArgs());
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args) => UpdateDisplay();

    public void ApplyLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);

        var v = s(StrId.GroupPhysical);
        if (v != null) PhysicalExpander.Header = v;
        v = s(StrId.GroupPagefile);
        if (v != null) PageFileExpander.Header = v;
        v = s(StrId.GroupSystemCache);
        if (v != null) CacheExpander.Header = v;

        var usage = s(StrId.ItemUsage);
        var avail = s(StrId.ItemAvailable);
        var total = s(StrId.ItemTotal);

        if (usage != null) { PhysicalUsageLabel.Text = usage + ":"; PageFileUsageLabel.Text = usage + ":"; CacheUsageLabel.Text = usage + ":"; }
        if (avail != null) { PhysicalFreeLabel.Text = avail + ":"; PageFileFreeLabel.Text = avail + ":"; CacheFreeLabel.Text = avail + ":"; }
        if (total != null) { PhysicalTotalLabel.Text = total + ":"; PageFileTotalLabel.Text = total + ":"; CacheTotalLabel.Text = total + ":"; }

        v = s(StrId.CleanMemory);
        if (v != null) CleanBtn.Content = v;

        if (App.MainWindow is MainWindow w)
            w.RefreshTrayMenu();
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

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:F2} GB",
        >= 1048576 => $"{bytes / 1048576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes} B"
    };

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
        CleanBtn.Content = (CoreService.GetString(StrId.CleanMemory) ?? "Cleaning") + "…";

        var result = await System.Threading.Tasks.Task.Run(() =>
            CoreService.CleanMemory(IniConfig.ReadUInt("ReductMask2", MemoryMask.Default)));
        CleanBtn.IsEnabled = true;

        ApplyLocalization();

        if (result.Success && result.BytesFreed > 0)
        {
            var title = CoreService.GetString(StrId.CleanMemory) ?? "Memory cleaned";
            ResultBar.Title = title;
            ResultBar.Message = $"Released: {result.FreedFormatted}";
            ResultBar.Severity = InfoBarSeverity.Success;

            if (IniConfig.ReadBool("BalloonCleanResults", true))
                ToastService.Show(title, $"Released: {result.FreedFormatted}");
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
