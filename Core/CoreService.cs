using static MemReduct.Core.NativeMethods;

namespace MemReduct.Core;

public enum CleanupSource : uint
{
    Auto = 0,
    Manual = 1,
    Hotkey = 2,
    CommandLine = 3
}

public static class MemoryMask
{
    public const uint WorkingSet = 0x01;
    public const uint SystemFileCache = 0x02;
    public const uint StandbyPriority0List = 0x04;
    public const uint StandbyList = 0x08;
    public const uint ModifiedList = 0x10;
    public const uint CombineMemoryLists = 0x20;
    public const uint RegistryCache = 0x40;
    public const uint ModifiedFileCache = 0x80;
    public const uint Default = WorkingSet | SystemFileCache | StandbyPriority0List | RegistryCache | CombineMemoryLists | ModifiedFileCache;
    public const uint All = 0xFF;
}

public class MemoryStats
{
    public ulong PhysicalTotal { get; set; }
    public ulong PhysicalUsed { get; set; }
    public ulong PhysicalFree { get; set; }
    public double PhysicalPercent { get; set; }
    public ulong PageFileTotal { get; set; }
    public ulong PageFileUsed { get; set; }
    public ulong PageFileFree { get; set; }
    public double PageFilePercent { get; set; }
    public ulong SystemCacheTotal { get; set; }
    public ulong SystemCacheUsed { get; set; }
    public ulong SystemCacheFree { get; set; }
    public double SystemCachePercent { get; set; }
}

public class CleanupResult
{
    public ulong BytesBefore { get; set; }
    public ulong BytesAfter { get; set; }
    public ulong BytesFreed { get; set; }
    public uint MaskUsed { get; set; }
    public string FreedFormatted { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public static class CoreService
{
    public static uint GetLimitValue() => core_get_limit_value();
    public static uint GetIntervalValue() => core_get_interval_value();
    public static uint GetDangerValue() => core_get_danger_value();
    public static uint GetWarningValue() => core_get_warning_value();
    public static uint GetConfigMask() => core_get_config_mask();
    public static bool IsElevated() => core_is_elevated();
    public static bool ShouldAutoClean() => core_should_autoclean();
    public static bool ShouldIntervalClean() => core_should_interval_clean();

    public static MemoryStats GetMemoryStats()
    {
        core_get_memory_info(
            out var pt, out var pu, out var pf, out var pp,
            out var gt, out var gu, out var gf, out var gp,
            out var ct, out var cu, out var cf, out var cp);

        return new MemoryStats
        {
            PhysicalTotal = pt,
            PhysicalUsed = pu,
            PhysicalFree = pf,
            PhysicalPercent = pp,
            PageFileTotal = gt,
            PageFileUsed = gu,
            PageFileFree = gf,
            PageFilePercent = gp,
            SystemCacheTotal = ct,
            SystemCacheUsed = cu,
            SystemCacheFree = cf,
            SystemCachePercent = cp
        };
    }

    public static CleanupResult CleanMemory(uint mask = 0)
    {
        var native = new CleanupResultNative();
        var success = core_clean_memory((uint)CleanupSource.Manual, mask, ref native);

        return new CleanupResult
        {
            BytesBefore = native.bytes_before,
            BytesAfter = native.bytes_after,
            BytesFreed = native.bytes_freed,
            MaskUsed = native.mask_used,
            FreedFormatted = native.formatted,
            Success = success
        };
    }
}
