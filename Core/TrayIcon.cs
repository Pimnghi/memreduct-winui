using System;
using System.Runtime.InteropServices;

namespace MemReduct.Core;

public static class TrayIcon
{
    private static readonly Guid IconGuid = new("AE9053F0-8D59-4803-9ABB-74AFE66B5FD2");
    private static nint _hwnd;
    private static bool _created;
    private static WndProcDelegate? _wndProcDelegate;

    private const uint WM_TRAYICON = 0x8001;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;
    private const uint NIF_GUID = 0x00000020;
    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIM_SETVERSION = 4;
    private const uint NIN_BALLOONUSERCLICK = 5;
    private const uint NIN_BALLOONTIMEOUT = 6;
    private const uint NIIF_NOSOUND = 0x00000010;
    private const uint NIIF_INFO = 0x00000001;

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32", SetLastError = true)]
    private static extern nint CreateWindowExW(uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        string? lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32")]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpWndClass);

    [DllImport("kernel32", SetLastError = true)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32")]
    private static extern nint LoadIconW(nint hInstance, nint lpIconName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion_or_Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public nint hIconSm;
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);

    public static event Action? TrayRightClick;

    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var evt = (uint)lParam & 0xFFFF;
            if (evt == NIN_BALLOONUSERCLICK || evt == NIN_BALLOONTIMEOUT)
                return 0;
            if (evt == 0x0205) // WM_RBUTTONUP
                TrayRightClick?.Invoke();
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public static bool Create(string tooltip)
    {
        if (_created) return true;

        _wndProcDelegate = WndProc;

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpszClassName = "MemReductTrayWnd",
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = GetModuleHandleW(null),
            hbrBackground = 16, // COLOR_WINDOW + 1
        };

        var atom = RegisterClassExW(ref wc);
        if (atom == 0) return false;

        _hwnd = CreateWindowExW(0, "MemReductTrayWnd", null, 0,
            0, 0, 0, 0, nint.Zero, nint.Zero, GetModuleHandleW(null), nint.Zero);
        if (_hwnd == nint.Zero) return false;

        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 0,
            guidItem = IconGuid,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_GUID,
            uCallbackMessage = WM_TRAYICON,
            hIcon = LoadIconW(nint.Zero, new nint(32512)), // IDI_APPLICATION
        };
        nid.szTip = tooltip;

        _created = Shell_NotifyIconW(NIM_ADD, ref nid);

        return _created;
    }

    public static void Destroy()
    {
        if (!_created) return;
        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 0,
            guidItem = IconGuid,
            uFlags = NIF_GUID,
        };
        Shell_NotifyIconW(NIM_DELETE, ref nid);
        if (_hwnd != nint.Zero) DestroyWindow(_hwnd);
        _created = false;
    }

    public static void ShowBalloon(string title, string text, bool noSound)
    {
        if (!_created) return;

        var flags = NIIF_INFO;
        if (noSound) flags |= NIIF_NOSOUND;

        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 0,
            guidItem = IconGuid,
            uFlags = NIF_INFO | NIF_GUID,
            szInfo = text,
            szInfoTitle = title,
            dwInfoFlags = flags,
        };
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }
}
