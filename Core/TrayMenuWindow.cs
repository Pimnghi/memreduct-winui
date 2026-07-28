using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace MemReduct.Core;

internal sealed class TrayMenuWindow : Window
{
    private readonly Grid _anchor;
    private readonly MenuFlyout _menu;
    private bool _closing;
    private bool _showRequested;
    private bool _menuOpen;
    private nint _hostHwnd;
    private nint _mouseHook;
    private nint _foregroundHook;
    private readonly LowLevelMouseProc _mouseHookProc;
    private readonly WinEventProc _foregroundHookProc;
    private readonly SubclassProc _subclassProc;
    private bool _subclassInstalled;
    private int _anchorX;
    private int _anchorY;

    private const uint GW_OWNER = 4;
    private const uint GA_ROOT = 2;
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const int WH_MOUSE_LL = 14;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private const int DWMWA_NCRENDERING_POLICY = 2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMNCRP_DISABLED = 1;
    private const int DWMWCP_DONOTROUND = 1;
    private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

    [DllImport("gdi32")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32")]
    private static extern int SetWindowRgn(nint hWnd, nint hRgn, bool bRedraw);

    [DllImport("gdi32")]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("comctl32", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        nint hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass,
        nuint dwRefData);

    [DllImport("comctl32")]
    private static extern bool RemoveWindowSubclass(
        nint hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32")]
    private static extern nint DefSubclassProc(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32")]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32")]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    [DllImport("dwmapi")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("dwmapi", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetWindowAttributeColor(
        nint hwnd,
        int attribute,
        ref uint value,
        int valueSize);

    [DllImport("user32", SetLastError = true)]
    private static extern nint SetWindowsHookExW(
        int idHook,
        LowLevelMouseProc lpfn,
        nint hmod,
        uint dwThreadId);

    [DllImport("user32")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32")]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint hmodWinEventProc,
        WinEventProc pfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32")]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32")]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32")]
    private static extern nint WindowFromPoint(POINT point);

    [DllImport("user32")]
    private static extern nint GetAncestor(nint hWnd, uint gaFlags);

    [DllImport("user32")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, char[] className, int maxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT Reserved;
        public POINT MaxSize;
        public POINT MaxPosition;
        public POINT MinTrackSize;
        public POINT MaxTrackSize;
    }

    private delegate nint LowLevelMouseProc(int nCode, nuint wParam, nint lParam);

    private delegate nint SubclassProc(
        nint hWnd,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData);

    private delegate void WinEventProc(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint eventTime);

    public TrayMenuWindow(
        bool dark,
        string showText,
        string cleanText,
        string settingsText,
        string exitText)
    {
        _mouseHookProc = OnLowLevelMouse;
        _foregroundHookProc = OnForegroundChanged;
        _subclassProc = HostSubclassProc;
        _anchor = new Grid
        {
            Width = 1,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light,
        };
        _anchor.Loaded += (_, _) => ShowMenuIfReady();
        Content = _anchor;

        _menu = new MenuFlyout();
        _menu.Items.Add(CreateCommandItem("\uE890", showText, TrayIcon.CMD_SHOW));
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(CreateCommandItem("\uE72C", cleanText, TrayIcon.CMD_CLEAN));
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(CreateRegionsSubmenu());
        _menu.Items.Add(CreateLimitSubmenu());
        _menu.Items.Add(CreateIntervalSubmenu());
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(CreateCommandItem("\uE713", settingsText, TrayIcon.CMD_SETTINGS));
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(CreateCommandItem("\uE7E8", exitText, TrayIcon.CMD_EXIT));

        // Do not destroy the XAML island from inside the flyout's own Closed
        // callback. CoreMessaging is still unwinding that popup at this point.
        _menu.Opening += (_, _) =>
        {
            _menuOpen = true;
            StartDismissMonitoring();
            DispatcherQueue.TryEnqueue(HideHostSurface);
        };
        _menu.Closed += (_, _) =>
        {
            _menuOpen = false;
            StopDismissMonitoring();
            DispatcherQueue.TryEnqueue(CloseHost);
        };
        Closed += (_, _) =>
        {
            StopDismissMonitoring();
            RemoveHostSubclass();
            if (!_closing)
                _menu.Hide();
        };
    }

    public void ShowAt(int cursorX, int cursorY)
    {
        _hostHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        InstallHostSubclass();

        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        var displayArea = DisplayArea.GetFromPoint(
            new PointInt32(cursorX, cursorY),
            DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        _anchorX = Math.Clamp(
            cursorX,
            workArea.X,
            Math.Max(workArea.X, workArea.X + workArea.Width - 2));
        _anchorY = Math.Clamp(
            cursorY,
            workArea.Y,
            Math.Max(workArea.Y, workArea.Y + workArea.Height - 2));

        AppWindow.MoveAndResize(new RectInt32(_anchorX, _anchorY, 1, 1));
        _showRequested = true;
        Activate();
        ForceHostToAnchor();
        DispatcherQueue.TryEnqueue(() =>
        {
            ForceHostToAnchor();
            ShowMenuIfReady();
        });
    }

    private void InstallHostSubclass()
    {
        if (_hostHwnd == nint.Zero || _subclassInstalled)
            return;

        _subclassInstalled = SetWindowSubclass(_hostHwnd, _subclassProc, 1, 0);
    }

    private void RemoveHostSubclass()
    {
        if (!_subclassInstalled || _hostHwnd == nint.Zero)
            return;

        RemoveWindowSubclass(_hostHwnd, _subclassProc, 1);
        _subclassInstalled = false;
    }

    private nint HostSubclassProc(
        nint hWnd,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WM_GETMINMAXINFO && lParam != nint.Zero)
        {
            var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            info.MinTrackSize.X = 1;
            info.MinTrackSize.Y = 1;
            Marshal.StructureToPtr(info, lParam, false);
            return 0;
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private void ForceHostToAnchor()
    {
        if (_hostHwnd == nint.Zero)
            return;

        SetWindowPos(
            _hostHwnd,
            nint.Zero,
            _anchorX,
            _anchorY,
            1,
            1,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOOWNERZORDER);

        var region = CreateRectRgn(0, 0, 1, 1);
        if (region != nint.Zero && SetWindowRgn(_hostHwnd, region, true) == 0)
            DeleteObject(region);

        SetWindowDisplayAffinity(_hostHwnd, WDA_EXCLUDEFROMCAPTURE);
    }

    private void HideHostSurface()
    {
        if (_hostHwnd == nint.Zero)
            return;

        var noNonClientRendering = DWMNCRP_DISABLED;
        DwmSetWindowAttribute(
            _hostHwnd,
            DWMWA_NCRENDERING_POLICY,
            ref noNonClientRendering,
            sizeof(int));

        var doNotRound = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(
            _hostHwnd,
            DWMWA_WINDOW_CORNER_PREFERENCE,
            ref doNotRound,
            sizeof(int));

        var noBorder = DWMWA_COLOR_NONE;
        DwmSetWindowAttributeColor(
            _hostHwnd,
            DWMWA_BORDER_COLOR,
            ref noBorder,
            sizeof(uint));

        var emptyRegion = CreateRectRgn(0, 0, 0, 0);
        if (emptyRegion != nint.Zero && SetWindowRgn(_hostHwnd, emptyRegion, true) == 0)
            DeleteObject(emptyRegion);
    }

    private void ShowMenuIfReady()
    {
        if (!_showRequested || _closing || _anchor.XamlRoot == null)
            return;

        _showRequested = false;
        _menu.ShowAt(_anchor, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.Auto,
            ShowMode = FlyoutShowMode.Standard,
        });
    }

    private static MenuFlyoutItem CreateCommandItem(string glyph, string text, int command)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = CreateIcon(glyph),
        };
        item.Click += (_, _) => TrayIcon.DispatchMenuCommand(command);
        return item;
    }

    private static MenuFlyoutSubItem CreateRegionsSubmenu()
    {
        var mask = IniConfig.ReadUInt("ReductMask2", MemoryMask.Default);
        var allowDangerousRegions = IniConfig.ReadBool("IsAllowStandbyListCleanup", false);
        var dangerousRegions = MemoryMask.StandbyList | MemoryMask.ModifiedList;
        if (!allowDangerousRegions && (mask & dangerousRegions) != 0)
        {
            mask &= ~dangerousRegions;
            IniConfig.WriteUInt("ReductMask2", mask);
        }

        var names = new[] { "Working set", "System file cache", "Modified file cache",
            "Modified page list", "Standby list", "Standby list (low)", "Registry cache", "Combine memory lists" };
        var masks = new uint[] { 0x01, 0x02, 0x80, 0x10, 0x08, 0x04, 0x40, 0x20 };
        var stringIds = new uint[] { 45, 46, 95, 49, 48, 47, 96, 50 };

        var submenu = new MenuFlyoutSubItem
        {
            Text = CoreService.GetString(StrId.TrayPopUp1) ?? "Clean areas",
            Icon = CreateIcon("\uEA37"),
        };
        for (var index = 0; index < names.Length; index++)
        {
            var itemMask = masks[index];
            var isDangerousRegion = (itemMask & dangerousRegions) != 0;
            var item = new ToggleMenuFlyoutItem
            {
                Text = CoreService.GetString(stringIds[index]) ?? names[index],
                IsChecked = (mask & itemMask) != 0,
                IsEnabled = allowDangerousRegions || !isDangerousRegion,
            };
            item.Click += (_, _) => IniConfig.WriteUInt("ReductMask2", mask ^ itemMask);
            submenu.Items.Add(item);
        }
        return submenu;
    }

    private static MenuFlyoutSubItem CreateLimitSubmenu()
    {
        var enabled = IniConfig.ReadBool("AutoreductEnable");
        var current = IniConfig.ReadUInt("AutoreductValue", 90);
        var submenu = new MenuFlyoutSubItem
        {
            Text = CoreService.GetString(StrId.TrayPopUp2) ?? "Clean when above",
            Icon = CreateIcon("\uE9D9"),
        };

        AddToggleItem(
            submenu,
            CoreService.GetString(StrId.TrayDisable) ?? "Disable",
            !enabled,
            () => IniConfig.WriteBool("AutoreductEnable", false));
        for (var value = 10; value <= 90; value += 10)
        {
            var selectedValue = value;
            AddToggleItem(
                submenu,
                $"{value}%",
                enabled && current == value,
                () =>
                {
                    IniConfig.WriteBool("AutoreductEnable", true);
                    IniConfig.WriteUInt("AutoreductValue", (uint)selectedValue);
                });
        }
        return submenu;
    }

    private static MenuFlyoutSubItem CreateIntervalSubmenu()
    {
        var enabled = IniConfig.ReadBool("AutoreductIntervalEnable");
        var current = IniConfig.ReadUInt("AutoreductIntervalValue", 30);
        var submenu = new MenuFlyoutSubItem
        {
            Text = CoreService.GetString(StrId.TrayPopUp3) ?? "Clean every",
            Icon = CreateIcon("\uE823"),
        };

        AddToggleItem(
            submenu,
            CoreService.GetString(StrId.TrayDisable) ?? "Disable",
            !enabled,
            () => IniConfig.WriteBool("AutoreductIntervalEnable", false));
        for (var value = 10; value <= 90; value += 10)
        {
            var selectedValue = value;
            var minuteUnit = CoreService.GetString(StrId.MinuteUnit) ?? "minutes";
            AddToggleItem(
                submenu,
                $"{value} {minuteUnit}",
                enabled && current == value,
                () =>
                {
                    IniConfig.WriteBool("AutoreductIntervalEnable", true);
                    IniConfig.WriteUInt("AutoreductIntervalValue", (uint)selectedValue);
                });
        }
        return submenu;
    }

    private static void AddToggleItem(
        MenuFlyoutSubItem submenu,
        string text,
        bool isChecked,
        Action action)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = text,
            IsChecked = isChecked,
        };
        item.Click += (_, _) =>
        {
            action();
            AutoCleanService.Refresh();
        };
        submenu.Items.Add(item);
    }

    private static FontIcon CreateIcon(string glyph) =>
        new()
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
        };

    private void StartDismissMonitoring()
    {
        if (_mouseHook == nint.Zero)
        {
            _mouseHook = SetWindowsHookExW(
                WH_MOUSE_LL,
                _mouseHookProc,
                GetModuleHandleW(null),
                0);
        }

        if (_foregroundHook == nint.Zero)
        {
            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                nint.Zero,
                _foregroundHookProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }
    }

    private void StopDismissMonitoring()
    {
        if (_mouseHook != nint.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = nint.Zero;
        }

        if (_foregroundHook != nint.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = nint.Zero;
        }
    }

    private nint OnLowLevelMouse(int nCode, nuint wParam, nint lParam)
    {
        if (nCode >= 0
            && (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN || wParam == WM_MBUTTONDOWN)
            && _menuOpen
            && !_closing)
        {
            var mouse = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if (!IsMenuWindow(WindowFromPoint(mouse.Point)))
                DispatcherQueue.TryEnqueue(DismissMenu);
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void OnForegroundChanged(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint eventTime)
    {
        if (_menuOpen && !_closing && !IsMenuWindow(hwnd))
            DispatcherQueue.TryEnqueue(DismissMenu);
    }

    private bool IsMenuWindow(nint window)
    {
        if (window == nint.Zero)
            return false;

        var root = GetAncestor(window, GA_ROOT);
        var current = root;
        while (current != nint.Zero)
        {
            if (current == _hostHwnd)
                return true;

            current = GetWindow(current, GW_OWNER);
        }

        GetWindowThreadProcessId(root, out var processId);
        if (processId != (uint)Environment.ProcessId)
            return false;

        var className = new char[128];
        var length = GetClassNameW(root, className, className.Length);
        if (length <= 0)
            return false;

        var name = new string(className, 0, length);
        return name.Contains("Popup", StringComparison.OrdinalIgnoreCase)
            || name.Equals("#32768", StringComparison.Ordinal);
    }

    private void DismissMenu()
    {
        if (_menuOpen && !_closing)
            _menu.Hide();
    }

    private void CloseHost()
    {
        if (_closing)
            return;

        _closing = true;
        Close();
    }
}
