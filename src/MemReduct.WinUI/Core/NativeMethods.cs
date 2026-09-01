using System;
using System.Runtime.InteropServices;

namespace MemReduct.WinUI.Core;

internal static class NativeMethods
{
    private const string DllName = "CoreLib.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool core_is_elevated();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void core_get_memory_info(
        out ulong physTotal, out ulong physUsed, out ulong physFree, out double physPct,
        out ulong pageTotal, out ulong pageUsed, out ulong pageFree, out double pagePct,
        out ulong cacheTotal, out ulong cacheUsed, out ulong cacheFree, out double cachePct);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool core_clean_memory(uint source, uint mask, ref CleanupResultNative result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint core_locale_count();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool core_locale_get_name(uint index, [Out] char[] buf, uint bufSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint core_locale_get_current();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool core_locale_set(nuint index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    internal static extern IntPtr core_get_string(uint uid);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct CleanupResultNative
{
    public ulong bytes_before;
    public ulong bytes_after;
    public ulong bytes_freed;
    public uint mask_used;
    public uint succeeded_mask;
    public uint failed_mask;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string? formatted;
}
