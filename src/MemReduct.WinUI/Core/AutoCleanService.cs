using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace MemReduct.WinUI.Core;

public static class AutoCleanService
{
    private static DispatcherQueueTimer? _timer;

    public static void Refresh()
    {
        var enabled = IniConfig.ReadBool("AutoreductEnable") || IniConfig.ReadBool("AutoreductIntervalEnable");
        if (enabled)
            Start();
        else
            Stop();
    }

    private static void Start()
    {
        if (_timer != null) return;

        _timer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_timer == null) return;

        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private static void Stop()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private static async void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (!CoreService.IsElevated()) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lastClean = IniConfig.ReadLong("StatisticLastReduct");
        var elapsed = Math.Max(0, now - lastClean);

        var thresholdEnabled = IniConfig.ReadBool("AutoreductEnable");
        var threshold = Math.Clamp(IniConfig.ReadUInt("AutoreductValue", 90), 1u, 100u);
        var thresholdReached = thresholdEnabled
            && elapsed >= 30
            && CoreService.GetMemoryStats().PhysicalPercent >= threshold;

        var intervalEnabled = IniConfig.ReadBool("AutoreductIntervalEnable");
        var interval = Math.Clamp(IniConfig.ReadUInt("AutoreductIntervalValue", 30), 1u, 1440u);
        var intervalReached = intervalEnabled && elapsed >= interval * 60L;

        var shouldClean = thresholdReached || intervalReached;
        if (!shouldClean) return;

        var result = await CleanupCoordinator.CleanAsync(
            CleanupSource.Auto,
            waitForTurn: false);

        if (result is { Success: true, BytesFreed: > 0 }
            && IniConfig.ReadBool("BalloonCleanResults", true))
            ToastService.ShowCleanResult(result);
    }
}
