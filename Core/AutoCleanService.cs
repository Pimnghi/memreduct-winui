using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace MemReduct.Core;

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

        var shouldClean = CoreService.ShouldAutoClean() || CoreService.ShouldIntervalClean();
        if (!shouldClean) return;

        var result = await Task.Run(() =>
            CoreService.CleanMemory(IniConfig.ReadUInt("ReductMask2", MemoryMask.Default)));

        if (result.Success && result.BytesFreed > 0 && IniConfig.ReadBool("BalloonCleanResults", true))
            ToastService.Show("Mem Reduct", $"Memory released: {result.FreedFormatted}");
    }
}
