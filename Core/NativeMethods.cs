using System.Runtime.InteropServices;

namespace MemReduct.Core;

internal static class NativeMethods
{
    private const string DllName = "CoreLib.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint core_get_limit_value();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint core_get_interval_value();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint core_get_danger_value();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint core_get_warning_value();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint core_get_config_mask();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool core_is_elevated();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool core_should_autoclean();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool core_should_interval_clean();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void core_get_memory_info(
        out ulong physTotal, out ulong physUsed, out ulong physFree, out double physPct,
        out ulong pageTotal, out ulong pageUsed, out ulong pageFree, out double pagePct,
        out ulong cacheTotal, out ulong cacheUsed, out ulong cacheFree, out double cachePct);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool core_clean_memory(uint source, uint mask, ref CleanupResultNative result);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct CleanupResultNative
{
    public ulong bytes_before;
    public ulong bytes_after;
    public ulong bytes_freed;
    public uint mask_used;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string formatted;
}
