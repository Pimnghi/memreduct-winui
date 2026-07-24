using MemReduct.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;

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

    public void SetCleaningState(bool cleaning)
    {
        if (cleaning)
        {
            CleanBtn.IsEnabled = false;
            CleanBtn.Content = (CoreService.GetString(StrId.CleanMemory) ?? "Cleaning") + "…";
        }
        else
        {
            CleanBtn.IsEnabled = true;
            CleanBtn.Content = CoreService.GetString(StrId.CleanMemory) ?? "Clean memory";
        }
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args) => UpdateDisplay();

    public void ApplyLocalization()
    {
        var s = (uint id) => CoreService.GetString(id);

        var v = s(StrId.GroupPhysical);
        if (v != null) PhysicalTitle.Text = v;
        v = s(StrId.GroupPagefile);
        if (v != null) PageFileTitle.Text = v;
        v = s(StrId.GroupSystemCache);
        if (v != null) CacheTitle.Text = v;

        var usage = s(StrId.ItemUsage);
        var avail = s(StrId.ItemAvailable);
        var total = s(StrId.ItemTotal);

        if (usage != null) { PhysicalPctLabel.Text = usage + ":"; PageFilePctLabel.Text = usage + ":"; CachePctLabel.Text = usage + ":"; }
        if (avail != null) { PhysicalFreeLabel.Text = avail + ":"; PageFileFreeLabel.Text = avail + ":"; CacheFreeLabel.Text = avail + ":"; }
        if (total != null) { PhysicalTotalLabel.Text = total + ":"; PageFileTotalLabel.Text = total + ":"; CacheTotalLabel.Text = total + ":"; }

        v = s(StrId.CleanMemory);
        if (v != null) CleanBtn.Content = v;
    }

    private void UpdateDisplay()
    {
        var stats = CoreService.GetMemoryStats();
        PhysicalBar.Value = stats.PhysicalPercent;
        PhysicalPctText.Text = $"{stats.PhysicalPercent:F1}%";
        PhysicalFreeText.Text = FormatBytes(stats.PhysicalFree);
        PhysicalTotalText.Text = FormatBytes(stats.PhysicalTotal);

        PageFileBar.Value = stats.PageFilePercent;
        PageFilePctText.Text = $"{stats.PageFilePercent:F1}%";
        PageFileFreeText.Text = FormatBytes(stats.PageFileFree);
        PageFileTotalText.Text = FormatBytes(stats.PageFileTotal);

        CacheBar.Value = stats.SystemCachePercent;
        CachePctText.Text = $"{stats.SystemCachePercent:F1}%";
        CacheFreeText.Text = FormatBytes(stats.SystemCacheFree);
        CacheTotalText.Text = FormatBytes(stats.SystemCacheTotal);

        UpdateBarColors(stats);
    }

    private void UpdateBarColors(MemoryStats stats)
    {
        var danger = IniConfig.ReadUInt("TrayLevelDanger", 90);
        var warning = IniConfig.ReadUInt("TrayLevelWarning", 70);

        var dangerBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xEC, 0x1C, 0x24));
        var warningBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x80, 0x40));

        SetBarColor(PhysicalBar, stats.PhysicalPercent, danger, warning, dangerBrush, warningBrush);
        SetBarColor(PageFileBar, stats.PageFilePercent, danger, warning, dangerBrush, warningBrush);
        SetBarColor(CacheBar, stats.SystemCachePercent, danger, warning, dangerBrush, warningBrush);
    }

    private static void SetBarColor(ProgressBar bar, double pct, uint danger, uint warning,
        Microsoft.UI.Xaml.Media.SolidColorBrush dangerBrush, Microsoft.UI.Xaml.Media.SolidColorBrush warningBrush)
    {
        if (pct >= danger)
            bar.Foreground = dangerBrush;
        else if (pct >= warning)
            bar.Foreground = warningBrush;
        else
            bar.ClearValue(ProgressBar.ForegroundProperty);
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
            ResultBar.Message = "Please run as administrator.";
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.IsOpen = true;

            var restart = new Button
            {
                Content = "Restart as Administrator",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8),
            };
            restart.Click += (s2, e2) =>
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(exePath)
                        {
                            UseShellExecute = true,
                            Verb = "runas",
                            WorkingDirectory = Path.GetDirectoryName(exePath),
                        });
                        Application.Current.Exit();
                    }
                    catch { }
                }
            };
            ResultBar.ActionButton = restart;
            return;
        }

        if (IniConfig.ReadBool("IsShowReductConfirmation", true) && XamlRoot != null)
        {
            var dialog = new ContentDialog
            {
                Title = CoreService.GetString(StrId.CleanMemory) ?? "Memory cleaning",
                Content = CoreService.GetString(StrId.Question) ?? "Are you sure?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        if (CleanBtn == null) return;

        CleanBtn.IsEnabled = false;
        CleanBtn.Content = (CoreService.GetString(StrId.CleanMemory) ?? "Cleaning") + "…";

        var result = await System.Threading.Tasks.Task.Run(() =>
            CoreService.CleanMemory(IniConfig.ReadUInt("ReductMask2", MemoryMask.Default)));
        CleanBtn.IsEnabled = true;

        ApplyLocalization();

        if (result.Success && result.BytesFreed > 0)
        {
            var msg = CoreService.GetString(StrId.StatusCleaned);
            ResultBar.Message = msg != null ? msg.Replace("%s", result.FreedFormatted) : $"Released: {result.FreedFormatted}";
            ResultBar.Severity = InfoBarSeverity.Success;

            if (IniConfig.ReadBool("BalloonCleanResults", true))
                ToastService.ShowCleanResult(result.BytesFreed, result.FreedFormatted);
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
