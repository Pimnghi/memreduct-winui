using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace MemReduct.Core;

public static class CleanupCoordinator
{
    private static readonly SemaphoreSlim CleanupGate = new(1, 1);
    private static readonly Mutex ProcessCleanupMutex = new(false, @"Local\MemReductWinUICleanup");

    public static async Task<CleanupResult?> CleanAsync(
        CleanupSource source,
        uint? requestedMask = null,
        bool waitForTurn = true,
        CancellationToken cancellationToken = default)
    {
        var entered = waitForTurn
            ? await WaitForTurnAsync(cancellationToken)
            : await CleanupGate.WaitAsync(0, cancellationToken);

        if (!entered)
            return null;

        try
        {
            var mask = requestedMask ?? IniConfig.ReadUInt("ReductMask2", MemoryMask.Default);
            if (source == CleanupSource.Auto && !IniConfig.ReadBool("IsAllowStandbyListCleanup"))
                mask &= ~(MemoryMask.StandbyList | MemoryMask.ModifiedList);

            if (mask == 0)
            {
                if (source == CleanupSource.Auto)
                    return null;

                var emptyResult = new CleanupResult
                {
                    Status = CleanupStatus.Failed,
                    ErrorMessage = "No memory cleanup areas are selected."
                };
                if (IniConfig.ReadBool("LogCleanResults"))
                    await AppendLogAsync(source, emptyResult);
                return emptyResult;
            }

            CleanupResult? result;
            try
            {
                result = await Task.Run(
                    () => CleanWithProcessMutex(mask, source, waitForTurn),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is DllNotFoundException
                                       or EntryPointNotFoundException
                                       or BadImageFormatException
                                       or SEHException)
            {
                result = new CleanupResult
                {
                    MaskUsed = mask,
                    FailedMask = mask,
                    Status = CleanupStatus.Failed,
                    ErrorMessage = ex.Message
                };
            }

            if (result == null)
                return null;

            if (result.SucceededMask != 0)
                IniConfig.WriteLong("StatisticLastReduct", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            if (IniConfig.ReadBool("LogCleanResults"))
                await AppendLogAsync(source, result);

            return result;
        }
        finally
        {
            CleanupGate.Release();
        }
    }

    private static async Task<bool> WaitForTurnAsync(CancellationToken cancellationToken)
    {
        await CleanupGate.WaitAsync(cancellationToken);
        return true;
    }

    private static CleanupResult? CleanWithProcessMutex(
        uint mask,
        CleanupSource source,
        bool waitForTurn)
    {
        var acquired = false;
        try
        {
            try
            {
                acquired = ProcessCleanupMutex.WaitOne(waitForTurn ? Timeout.Infinite : 0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            return acquired ? CoreService.CleanMemory(mask, source) : null;
        }
        finally
        {
            if (acquired)
                ProcessCleanupMutex.ReleaseMutex();
        }
    }

    private static async Task AppendLogAsync(CleanupSource source, CleanupResult result)
    {
        try
        {
            Directory.CreateDirectory(IniConfig.DataDirectory);
            var line = string.Join(
                '\t',
                DateTimeOffset.Now.ToString("O"),
                source,
                result.Status,
                $"requested=0x{result.MaskUsed:X2}",
                $"succeeded=0x{result.SucceededMask:X2}",
                $"failed=0x{result.FailedMask:X2}",
                $"freed={result.BytesFreed}",
                result.ErrorMessage ?? string.Empty);
            await File.AppendAllTextAsync(
                Path.Combine(IniConfig.DataDirectory, "memreduct-winui.log"),
                line + Environment.NewLine,
                Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
