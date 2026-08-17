using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace MemReduct.Core;

public static class TrayIcon
{
    private static readonly Guid IconGuid = new("B5F8C3A1-2D4E-4F6A-8C9B-1E3D5F7A9B2C");
    private static readonly object IconSync = new();
    private static nint _hwnd;
    private static bool _created;
    private static WndProcDelegate? _wndProcDelegate;
    private static uint _taskbarCreatedMessage;
    private static bool _useDarkMenu;

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
    public const int ACTION_SHOW = 0;
    public const int ACTION_CLEAN = 1;
    public const int ACTION_TASKMGR = 2;

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

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

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

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
    public static event Action<int, int>? ContextMenuRequested;

    private static string _textShow = "Show / Hide";
    private static string _textClean = "Clean memory";
    private static string _textSettings = "Settings";
    private static string _textExit = "Exit";

    private static nint _currentIcon;
    private static bool _ownsCurrentIcon;
    private static bool _usingMemoryIcon;
    private static string? _iconPath;
    private static string _tooltip = "Mem Reduct WinUI";
    private static Timer? _memoryTimer;
    private static int _memoryRefreshActive;
    private static int _lastIconSignature = -1;

    public static void SetMenuTexts(string show, string clean, string settings, string exit)
    {
        _textShow = show;
        _textClean = clean;
        _textSettings = settings;
        _textExit = exit;
    }

    public static void SetMenuTheme(bool useDarkTheme)
    {
        _useDarkMenu = useDarkTheme;
    }

    internal static bool UseDarkMenu => _useDarkMenu;

    internal static string ShowMenuText => _textShow;
    internal static string CleanMenuText => _textClean;
    internal static string SettingsMenuText => _textSettings;
    internal static string ExitMenuText => _textExit;

    internal static void DispatchMenuCommand(int command)
    {
        TrayCommand?.Invoke(command);
    }

    public static void SetIcon(string path)
    {
        _iconPath = path;
        if (_created)
        {
            RefreshMemoryDisplay(forceIcon: true);
        }
    }

    public static void RefreshMemoryDisplay()
    {
        RefreshMemoryDisplay(forceIcon: false);
    }

    private static void RefreshMemoryDisplay(bool forceIcon)
    {
        if (Interlocked.Exchange(ref _memoryRefreshActive, 1) != 0)
            return;

        try
        {
            var stats = CoreService.GetMemoryStats();
            var precisePercent = Math.Clamp(stats.PhysicalPercent, 0, 100);
            var physicalLabel = CoreService.GetString(StrId.GroupPhysical) ?? "Physical memory";
            var tooltip = $"{physicalLabel}: {precisePercent.ToString("F1", CultureInfo.CurrentCulture)}%";
            var showMemoryUsage = IniConfig.ReadBool("TrayShowMemoryUsage", false);
            var roundedPercent = (int)Math.Round(precisePercent, MidpointRounding.AwayFromZero);
            var danger = IniConfig.ReadUInt("TrayLevelDanger", 90);
            var warning = IniConfig.ReadUInt("TrayLevelWarning", 70);
            var severity = roundedPercent >= danger ? 2 : roundedPercent >= warning ? 1 : 0;
            var signature = roundedPercent | (severity << 16);

            lock (IconSync)
            {
                if (!_created || _hwnd == nint.Zero)
                    return;

                var replaceIcon = forceIcon || showMemoryUsage != _usingMemoryIcon ||
                    (showMemoryUsage && signature != _lastIconSignature);
                if (!replaceIcon && string.Equals(_tooltip, tooltip, StringComparison.Ordinal))
                    return;

                var newIcon = nint.Zero;
                var ownsNewIcon = false;
                if (replaceIcon)
                {
                    if (showMemoryUsage)
                    {
                        newIcon = TrayMemoryIcon.Create(roundedPercent, severity);
                        ownsNewIcon = newIcon != nint.Zero;
                    }

                    if (newIcon == nint.Zero)
                        newIcon = LoadTrayIcon(out ownsNewIcon);
                }

                var nid = new NOTIFYICONDATAW
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                    hWnd = _hwnd,
                    uID = 0,
                    guidItem = IconGuid,
                    uFlags = NIF_TIP | NIF_GUID | (replaceIcon ? NIF_ICON : 0),
                    hIcon = newIcon,
                    szTip = tooltip,
                };
                if (!Shell_NotifyIconW(NIM_MODIFY, ref nid))
                {
                    if (ownsNewIcon && newIcon != nint.Zero)
                        DestroyIcon(newIcon);
                    return;
                }

                _tooltip = tooltip;
                if (replaceIcon)
                {
                    ReplaceCurrentIcon(newIcon, ownsNewIcon);
                    _usingMemoryIcon = showMemoryUsage;
                    _lastIconSignature = showMemoryUsage ? signature : -1;
                }
            }
        }
        catch
        {
            // Tray monitoring is optional and must never terminate the application.
        }
        finally
        {
            Volatile.Write(ref _memoryRefreshActive, 0);
        }
    }

    private static void OnMemoryTimer(object? state)
    {
        _ = state;
        RefreshMemoryDisplay(forceIcon: false);
    }

    private static void ReplaceCurrentIcon(nint newIcon, bool ownsNewIcon)
    {
        if (_ownsCurrentIcon && _currentIcon != nint.Zero && _currentIcon != newIcon)
            DestroyIcon(_currentIcon);
        _currentIcon = newIcon;
        _ownsCurrentIcon = ownsNewIcon;
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
                GetCursorPos(out var cursor);
                ContextMenuRequested?.Invoke(cursor.X, cursor.Y);
                return 0;
            }
        }
        else if (msg == WM_HOTKEY && wParam == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            return 0;
        }
        else if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
        {
            _created = AddTrayIcon();
            if (_created)
                RefreshMemoryDisplay(forceIcon: true);
            return 0;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public static bool Create(string tooltip)
    {
        if (_created) return true;

        _wndProcDelegate = WndProc;
        _tooltip = tooltip;
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

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

        _created = AddTrayIcon();
        if (!_created)
        {
            DestroyWindow(_hwnd);
            _hwnd = nint.Zero;
        }
        else
        {
            _memoryTimer = new Timer(
                OnMemoryTimer,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2));
        }

        return _created;
    }

    public static bool RefreshHotkey()
    {
        if (_hwnd == nint.Zero) return false;
        UnregisterHotKey(_hwnd, (int)HOTKEY_ID);

        var enabled = IniConfig.ReadBool("HotkeyCleanEnable");
        if (!enabled) return true;

        var hotkey = IniConfig.ReadInt("HotkeyClean", (int)(0x0002 << 8 | VK_F1));
        var vk = (uint)(hotkey & 0xFF);
        var mod = (uint)((hotkey >> 8) & 0xFF);

        uint winMod = 0;
        if ((mod & 1) != 0) winMod |= 0x0004;
        if ((mod & 2) != 0) winMod |= 0x0002;
        if ((mod & 4) != 0) winMod |= 0x0001;
        if ((mod & 8) != 0) winMod |= 0x0008;

        return RegisterHotKey(_hwnd, (int)HOTKEY_ID, winMod, vk);
    }

    public static void Destroy()
    {
        var timer = Interlocked.Exchange(ref _memoryTimer, null);
        timer?.Dispose();

        lock (IconSync)
        {
            if (_hwnd != nint.Zero) UnregisterHotKey(_hwnd, (int)HOTKEY_ID);
            if (_created)
            {
                var nid = new NOTIFYICONDATAW
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                    hWnd = _hwnd,
                    uID = 0,
                    guidItem = IconGuid,
                    uFlags = NIF_GUID,
                };
                Shell_NotifyIconW(NIM_DELETE, ref nid);
            }
            _created = false;
            if (_hwnd != nint.Zero) DestroyWindow(_hwnd);
            if (_ownsCurrentIcon && _currentIcon != nint.Zero) DestroyIcon(_currentIcon);
            _hwnd = nint.Zero;
            _currentIcon = nint.Zero;
            _ownsCurrentIcon = false;
            _usingMemoryIcon = false;
            _lastIconSignature = -1;
        }
    }

    private static bool AddTrayIcon()
    {
        lock (IconSync)
        {
            var reusingCurrentIcon = _currentIcon != nint.Zero;
            var ownsIcon = false;
            var icon = reusingCurrentIcon ? _currentIcon : LoadTrayIcon(out ownsIcon);
            if (reusingCurrentIcon)
                ownsIcon = _ownsCurrentIcon;
            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hwnd,
                uID = 0,
                guidItem = IconGuid,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_GUID,
                uCallbackMessage = WM_TRAYICON,
                hIcon = icon,
                szTip = _tooltip,
            };

            if (!Shell_NotifyIconW(NIM_ADD, ref nid))
            {
                if (!reusingCurrentIcon && ownsIcon && icon != nint.Zero)
                    DestroyIcon(icon);
                return false;
            }

            if (!reusingCurrentIcon)
                ReplaceCurrentIcon(icon, ownsIcon);
            return true;
        }
    }

    private static nint LoadTrayIcon(out bool ownsIcon)
    {
        var icon = nint.Zero;
        if (_iconPath != null)
            icon = LoadImageW(nint.Zero, _iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

        ownsIcon = icon != nint.Zero;
        return ownsIcon ? icon : LoadIconW(nint.Zero, new nint(32512));
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
