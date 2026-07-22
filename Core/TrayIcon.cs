using System;
using System.Runtime.InteropServices;

namespace MemReduct.Core;

public static class TrayIcon
{
    private static readonly Guid IconGuid = new("B5F8C3A1-2D4E-4F6A-8C9B-1E3D5F7A9B2C");
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
    private const uint NIN_BALLOONUSERCLICK = 5;
    private const uint NIN_BALLOONTIMEOUT = 6;
    private const uint NIIF_NOSOUND = 0x00000010;
    private const uint NIIF_INFO = 0x00000001;
    private const uint WM_HOTKEY = 0x0312;
    private const uint HOTKEY_ID = 1337;
    private const uint VK_F1 = 0x70;

    public const int CMD_SHOW = 1;
    public const int CMD_CLEAN = 2;
    public const int CMD_SETTINGS = 3;
    public const int CMD_EXIT = 4;
    private const int CMD_REGION_BASE = 100;
    private const int CMD_LIMIT_BASE = 200;
    private const int CMD_INTERVAL_BASE = 300;

    public const int ACTION_SHOW = 0;
    public const int ACTION_CLEAN = 1;
    public const int ACTION_TASKMGR = 2;

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(nint hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);

    [DllImport("user32")]
    private static extern bool CheckMenuItem(nint hMenu, nuint uIDCheckItem, uint uCheck);

    [DllImport("user32")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32")]
    private static extern int TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [DllImport("user32")]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32")]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

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

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern nint LoadImageW(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32")]
    private static extern bool DestroyIcon(nint hIcon);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE = 0x0040;

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

    public static event Action<int>? TrayCommand;
    public static event Action? HotkeyPressed;
    public static event Action<int>? TrayClickAction;

    private static string _textShow = "Show / Hide";
    private static string _textClean = "Clean memory";
    private static string _textSettings = "Settings";
    private static string _textExit = "Exit";

    private static string _textRegions = "Clean areas";
    private static string _textLimit = "Clean when above";
    private static string _textInterval = "Clean every";
    private static string _textDisable = "Disable";
    private static string _textMinutes = " min.";

    private static readonly string[] _regionNames = { "Working set", "System file cache", "Modified file cache",
        "Modified page list", "Standby list", "Standby list (low)", "Registry cache", "Combine memory lists" };
    private static readonly uint[] _regionMasks = { 0x01, 0x02, 0x80, 0x10, 0x08, 0x04, 0x40, 0x20 };

    private static string _actionShowHide = "Show / Hide";
    private static string _actionClean = "Clean memory"; 
    private static string _actionTaskmgr = "Open task manager";

    private static nint _currentIcon;
    private static string? _iconPath;

    public static void SetMenuTexts(string show, string clean, string settings, string exit)
    {
        _textShow = show;
        _textClean = clean;
        _textSettings = settings;
        _textExit = exit;
    }

    public static void SetIcon(string path)
    {
        _iconPath = path;
        if (_created)
        {
            UpdateTrayIcon();
        }
    }

    private static void UpdateTrayIcon()
    {
        var newIcon = nint.Zero;
        if (_iconPath != null)
            newIcon = LoadImageW(nint.Zero, _iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
        if (newIcon == nint.Zero)
            newIcon = LoadIconW(nint.Zero, new nint(32512)); // IDI_APPLICATION fallback

        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 0,
            guidItem = IconGuid,
            uFlags = NIF_ICON | NIF_GUID,
            hIcon = newIcon,
        };
        Shell_NotifyIconW(NIM_MODIFY, ref nid);

        if (_currentIcon != nint.Zero && _currentIcon != LoadIconW(nint.Zero, new nint(32512)))
            DestroyIcon(_currentIcon);
        _currentIcon = newIcon;
    }

    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var evt = (uint)lParam & 0xFFFF;
            if (evt == NIN_BALLOONUSERCLICK || evt == NIN_BALLOONTIMEOUT)
                return 0;

            if (evt == 0x0201) // WM_LBUTTONDOWN
            {
                var action = IniConfig.ReadInt("TrayActionDc", ACTION_SHOW);
                TrayClickAction?.Invoke(action);
                return 0;
            }
            if (evt == 0x0207) // WM_MBUTTONDOWN
            {
                var action = IniConfig.ReadInt("TrayActionMc", ACTION_CLEAN);
                TrayClickAction?.Invoke(action);
                return 0;
            }
            if (evt == 0x0205) // WM_RBUTTONUP
            {
                ShowContextMenu(hwnd);
                return 0;
            }
        }
        else if (msg == WM_HOTKEY && wParam == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            return 0;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static void ShowContextMenu(nint hwnd)
    {
        var hMenu = CreatePopupMenu();
        var mask = IniConfig.ReadUInt("ReductMask2", 0xE7);
        var limitEnabled = IniConfig.ReadBool("AutoreductEnable");
        var intervalEnabled = IniConfig.ReadBool("AutoreductIntervalEnable");
        var limitVal = IniConfig.ReadUInt("AutoreductValue", 90);
        var intervalVal = IniConfig.ReadUInt("AutoreductIntervalValue", 30);

        var sRegions = CoreService.GetString(14) ?? "Clean areas";
        var sLimit = CoreService.GetString(15) ?? "Clean when above";
        var sInterval = CoreService.GetString(16) ?? "Clean every";
        var sDisable = CoreService.GetString(13) ?? "Disable";

        AppendMenuW(hMenu, 0, CMD_SHOW, _textShow);
        AppendMenuW(hMenu, 0x800, 0, "");
        AppendMenuW(hMenu, 0, CMD_CLEAN, _textClean);
        AppendMenuW(hMenu, 0x800, 0, "");

        // Regions submenu
        var hRegion = CreatePopupMenu();
        for (int i = 0; i < _regionNames.Length; i++)
        {
            var id = CMD_REGION_BASE + i;
            var rname = CoreService.GetString((uint)(45 + i)) ?? _regionNames[i];
            AppendMenuW(hRegion, 0, (nuint)id, rname);
            if ((mask & _regionMasks[i]) != 0)
                CheckMenuItem(hRegion, (nuint)id, 8);
        }
        AppendMenuW(hMenu, 0x10, (nuint)hRegion, sRegions);

        // Clean when above submenu
        var hLimit = CreatePopupMenu();
        AppendMenuW(hLimit, 0, CMD_LIMIT_BASE, sDisable);
        if (!limitEnabled) CheckMenuItem(hLimit, (nuint)CMD_LIMIT_BASE, 8);
        for (int p = 10; p <= 90; p += 10)
        {
            var id = CMD_LIMIT_BASE + p;
            AppendMenuW(hLimit, 0, (nuint)id, $"{p}%");
            if (limitEnabled && limitVal == p) CheckMenuItem(hLimit, (nuint)id, 8);
        }
        AppendMenuW(hMenu, 0x10, (nuint)hLimit, sLimit);

        // Clean every submenu
        var hInterval = CreatePopupMenu();
        AppendMenuW(hInterval, 0, CMD_INTERVAL_BASE, sDisable);
        if (!intervalEnabled) CheckMenuItem(hInterval, (nuint)CMD_INTERVAL_BASE, 8);
        for (int m = 10; m <= 90; m += 10)
        {
            var id = CMD_INTERVAL_BASE + m;
            AppendMenuW(hInterval, 0, (nuint)id, $"{m} min.");
            if (intervalEnabled && intervalVal == m) CheckMenuItem(hInterval, (nuint)id, 8);
        }
        AppendMenuW(hMenu, 0x10, (nuint)hInterval, sInterval);

        AppendMenuW(hMenu, 0x800, 0, "");
        AppendMenuW(hMenu, 0, CMD_SETTINGS, _textSettings);
        AppendMenuW(hMenu, 0x800, 0, "");
        AppendMenuW(hMenu, 0, CMD_EXIT, _textExit);

        SetForegroundWindow(hwnd);
        GetCursorPos(out var pt);
        var cmd = TrackPopupMenu(hMenu, 0x2 | 0x100 | 0x80, pt.X, pt.Y, 0, hwnd, 0);

        if (cmd == 0) return;

        if (cmd >= CMD_REGION_BASE && cmd < CMD_LIMIT_BASE)
        {
            var idx = cmd - CMD_REGION_BASE;
            IniConfig.WriteUInt("ReductMask2", mask ^ _regionMasks[idx]);
        }
        else if (cmd >= CMD_LIMIT_BASE && cmd < CMD_INTERVAL_BASE)
        {
            var pct = cmd - CMD_LIMIT_BASE;
            if (pct == 0) { IniConfig.WriteBool("AutoreductEnable", false); }
            else { IniConfig.WriteBool("AutoreductEnable", true); IniConfig.WriteUInt("AutoreductValue", (uint)pct); }
            AutoCleanService.Refresh();
        }
        else if (cmd >= CMD_INTERVAL_BASE)
        {
            var min = cmd - CMD_INTERVAL_BASE;
            if (min == 0) { IniConfig.WriteBool("AutoreductIntervalEnable", false); }
            else { IniConfig.WriteBool("AutoreductIntervalEnable", true); IniConfig.WriteUInt("AutoreductIntervalValue", (uint)min); }
            AutoCleanService.Refresh();
        }
        else
        {
            TrayCommand?.Invoke(cmd);
        }
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
            hbrBackground = 16,
        };

        var atom = RegisterClassExW(ref wc);
        if (atom == 0) return false;

        _hwnd = CreateWindowExW(0, "MemReductTrayWnd", null, 0,
            0, 0, 0, 0, nint.Zero, nint.Zero, GetModuleHandleW(null), nint.Zero);
        if (_hwnd == nint.Zero) return false;

        var icon = nint.Zero;
        if (_iconPath != null)
            icon = LoadImageW(nint.Zero, _iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
        if (icon == nint.Zero)
            icon = LoadIconW(nint.Zero, new nint(32512));

        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 0,
            guidItem = IconGuid,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_GUID,
            uCallbackMessage = WM_TRAYICON,
            hIcon = icon,
        };
        nid.szTip = tooltip;

        _created = Shell_NotifyIconW(NIM_ADD, ref nid);
        _currentIcon = icon;

        return _created;
    }

    public static void RefreshHotkey()
    {
        if (_hwnd == nint.Zero) return;
        UnregisterHotKey(_hwnd, (int)HOTKEY_ID);

        var enabled = IniConfig.ReadBool("HotkeyCleanEnable");
        if (!enabled) return;

        var hotkey = IniConfig.ReadInt("HotkeyClean", (int)(0x0002 << 8 | VK_F1));
        var vk = (uint)(hotkey & 0xFF);
        var mod = (uint)((hotkey >> 8) & 0xFF);

        uint winMod = 0;
        if ((mod & 1) != 0) winMod |= 0x0004;
        if ((mod & 2) != 0) winMod |= 0x0002;
        if ((mod & 4) != 0) winMod |= 0x0001;
        if ((mod & 8) != 0) winMod |= 0x0008;

        RegisterHotKey(_hwnd, (int)HOTKEY_ID, winMod, vk);
    }

    public static void Destroy()
    {
        if (!_created) return;
        if (_hwnd != nint.Zero) UnregisterHotKey(_hwnd, (int)HOTKEY_ID);
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
        if (_currentIcon != nint.Zero) DestroyIcon(_currentIcon);
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
