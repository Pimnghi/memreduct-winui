using MemReduct.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        CleanBtn.IsEnabled = !cleaning;
        CleanButtonIdleContent.Visibility = cleaning ? Visibility.Collapsed : Visibility.Visible;
        CleanButtonBusyContent.Visibility = cleaning ? Visibility.Visible : Visibility.Collapsed;
        CleanProgressRing.IsActive = cleaning;
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
        var available = s(StrId.ItemAvailable);
        var total = s(StrId.ItemTotal);

        if (usage != null)
        {
            PhysicalPctLabel.Text = usage + ":";
            PageFilePctLabel.Text = usage + ":";
            CachePctLabel.Text = usage + ":";
        }

        if (available != null)
        {
            PhysicalFreeLabel.Text = available + ":";
            PageFileFreeLabel.Text = available + ":";
            CacheFreeLabel.Text = available + ":";
        }

        if (total != null)
        {
            PhysicalTotalLabel.Text = total + ":";
            PageFileTotalLabel.Text = total + ":";
            CacheTotalLabel.Text = total + ":";
        }

        var cleanText = s(StrId.CleanMemory) ?? "Clean memory";
        CleanButtonText.Text = cleanText;
        CleaningButtonText.Text = cleanText + "…";
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

        UpdateUsageColors(stats);
    }

    private void UpdateUsageColors(MemoryStats stats)
    {
        var danger = IniConfig.ReadUInt("TrayLevelDanger", 90);
        var warning = IniConfig.ReadUInt("TrayLevelWarning", 70);
        var dangerBrush = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        var warningBrush = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"];

        SetUsageColor(PhysicalBar, PhysicalPctText, stats.PhysicalPercent, danger, warning, dangerBrush, warningBrush);
        SetUsageColor(PageFileBar, PageFilePctText, stats.PageFilePercent, danger, warning, dangerBrush, warningBrush);
        SetUsageColor(CacheBar, CachePctText, stats.SystemCachePercent, danger, warning, dangerBrush, warningBrush);
    }

    private static void SetUsageColor(
        ProgressBar bar,
        TextBlock percentageText,
        double percentage,
        uint danger,
        uint warning,
        Brush dangerBrush,
        Brush warningBrush)
    {
        if (percentage >= danger)
        {
            bar.Foreground = dangerBrush;
            percentageText.Foreground = dangerBrush;
        }
        else if (percentage >= warning)
        {
            bar.Foreground = warningBrush;
            percentageText.Foreground = warningBrush;
        }
        else
        {
            bar.ClearValue(ProgressBar.ForegroundProperty);
            percentageText.ClearValue(TextBlock.ForegroundProperty);
        }
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
                if (string.IsNullOrEmpty(exePath)) return;

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
                catch
                {
                    // The user can dismiss the InfoBar if elevation is cancelled.
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

        ResultBar.IsOpen = false;
        ResultBar.ActionButton = null;
        SetCleaningState(true);

        CleanupResult? result;
        try
        {
            result = await CleanupCoordinator.CleanAsync(CleanupSource.Manual);
        }
        catch (Exception ex)
        {
            ResultBar.Title = "Memory cleaning failed";
            ResultBar.Message = ex.Message;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.IsOpen = true;
            return;
        }
        finally
        {
            SetCleaningState(false);
            ApplyLocalization();
        }

        if (result is null)
        {
            ResultBar.Title = CoreService.GetString(StrId.CleanMemory) ?? "Memory cleaning";
            ResultBar.Message = "The cleanup request could not be started.";
            ResultBar.Severity = InfoBarSeverity.Warning;
            ResultBar.IsOpen = true;
            return;
        }

        if (result is { Success: true, BytesFreed: > 0 })
        {
            var message = CoreService.GetString(StrId.StatusCleaned);
            ResultBar.Title = string.Empty;
            ResultBar.Message = message != null
                ? message.Replace("%s", result.FreedFormatted)
                : $"Released: {result.FreedFormatted}";
            ResultBar.Severity = result.Status == CleanupStatus.PartialSuccess
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Success;

            if (IniConfig.ReadBool("BalloonCleanResults", true))
                ToastService.ShowCleanResult(result.BytesFreed, result.FreedFormatted);
        }
        else if (result.Status == CleanupStatus.Failed)
        {
            ResultBar.Title = "Memory cleaning failed";
            ResultBar.Message = result.ErrorMessage ?? $"Failed areas: 0x{result.FailedMask:X2}";
            ResultBar.Severity = InfoBarSeverity.Error;
        }
        else
        {
            ResultBar.Title = string.Empty;
            ResultBar.Message = "No significant memory was released.";
            ResultBar.Severity = InfoBarSeverity.Informational;
        }

        ResultBar.IsOpen = true;
    }
}
