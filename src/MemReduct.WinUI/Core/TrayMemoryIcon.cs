using System;
using System.Runtime.InteropServices;

namespace MemReduct.WinUI.Core;

internal static class TrayMemoryIcon
{
    private const int IconSize = 32;
    private const int Transparent = 1;
    private const int FontWeightSemibold = 600;
    private const int ColorHighlight = 13;
    private const uint DtCenter = 0x00000001;
    private const uint DtVCenter = 0x00000004;
    private const uint DtSingleLine = 0x00000020;
    private const uint DtNoPrefix = 0x00000800;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public uint XHotspot;
        public uint YHotspot;
        public nint MaskBitmap;
        public nint ColorBitmap;
    }

    [DllImport("user32")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32")]
    private static extern int ReleaseDC(nint hwnd, nint dc);

    [DllImport("user32")]
    private static extern uint GetSysColor(int index);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern int DrawTextW(nint dc, string text, int textLength, ref Rect rect, uint format);

    [DllImport("user32")]
    private static extern nint CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("gdi32")]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32")]
    private static extern bool DeleteDC(nint dc);

    [DllImport("gdi32")]
    private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);

    [DllImport("gdi32")]
    private static extern nint CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, nint bits);

    [DllImport("gdi32")]
    private static extern nint SelectObject(nint dc, nint obj);

    [DllImport("gdi32")]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32")]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("gdi32")]
    private static extern nint CreatePen(int style, int width, uint color);

    [DllImport("gdi32")]
    private static extern bool Rectangle(nint dc, int left, int top, int right, int bottom);

    [DllImport("gdi32")]
    private static extern bool RoundRect(nint dc, int left, int top, int right, int bottom, int width, int height);

    [DllImport("gdi32")]
    private static extern int SetBkMode(nint dc, int mode);

    [DllImport("gdi32")]
    private static extern uint SetTextColor(nint dc, uint color);

    [DllImport("gdi32", CharSet = CharSet.Unicode)]
    private static extern nint CreateFontW(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint charSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    internal static nint Create(int percentage, int severity)
    {
        percentage = Math.Clamp(percentage, 0, 100);
        var text = percentage.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var background = severity switch
        {
            2 => Rgb(196, 43, 28),
            1 => Rgb(249, 168, 37),
            _ => GetSysColor(ColorHighlight),
        };
        var foreground = HasLightBackground(background) ? Rgb(0, 0, 0) : Rgb(255, 255, 255);

        var screenDc = GetDC(nint.Zero);
        if (screenDc == nint.Zero)
            return nint.Zero;

        nint colorDc = nint.Zero;
        nint maskDc = nint.Zero;
        nint colorBitmap = nint.Zero;
        nint maskBitmap = nint.Zero;
        nint backgroundBrush = nint.Zero;
        nint backgroundPen = nint.Zero;
        nint clearBrush = nint.Zero;
        nint clearPen = nint.Zero;
        nint maskWhiteBrush = nint.Zero;
        nint maskWhitePen = nint.Zero;
        nint maskBlackBrush = nint.Zero;
        nint maskBlackPen = nint.Zero;
        nint font = nint.Zero;
        nint oldColorBitmap = nint.Zero;
        nint oldMaskBitmap = nint.Zero;
        nint oldColorBrush = nint.Zero;
        nint oldColorPen = nint.Zero;
        nint oldMaskBrush = nint.Zero;
        nint oldMaskPen = nint.Zero;
        nint oldFont = nint.Zero;

        try
        {
            colorDc = CreateCompatibleDC(screenDc);
            maskDc = CreateCompatibleDC(screenDc);
            colorBitmap = CreateCompatibleBitmap(screenDc, IconSize, IconSize);
            maskBitmap = CreateBitmap(IconSize, IconSize, 1, 1, nint.Zero);
            if (colorDc == nint.Zero || maskDc == nint.Zero ||
                colorBitmap == nint.Zero || maskBitmap == nint.Zero)
            {
                return nint.Zero;
            }

            oldColorBitmap = SelectObject(colorDc, colorBitmap);
            oldMaskBitmap = SelectObject(maskDc, maskBitmap);

            // Match the color bitmap outside the rounded mask to the badge color.
            // This prevents one-pixel black fringes when Shell scales the icon.
            clearBrush = CreateSolidBrush(background);
            clearPen = CreatePen(0, 1, background);
            oldColorBrush = SelectObject(colorDc, clearBrush);
            oldColorPen = SelectObject(colorDc, clearPen);
            Rectangle(colorDc, 0, 0, IconSize, IconSize);

            backgroundBrush = CreateSolidBrush(background);
            backgroundPen = CreatePen(0, 1, background);
            SelectObject(colorDc, backgroundBrush);
            SelectObject(colorDc, backgroundPen);
            RoundRect(colorDc, 1, 1, IconSize - 1, IconSize - 1, 9, 9);

            maskWhiteBrush = CreateSolidBrush(Rgb(255, 255, 255));
            maskWhitePen = CreatePen(0, 1, Rgb(255, 255, 255));
            oldMaskBrush = SelectObject(maskDc, maskWhiteBrush);
            oldMaskPen = SelectObject(maskDc, maskWhitePen);
            Rectangle(maskDc, 0, 0, IconSize, IconSize);

            maskBlackBrush = CreateSolidBrush(Rgb(0, 0, 0));
            maskBlackPen = CreatePen(0, 1, Rgb(0, 0, 0));
            SelectObject(maskDc, maskBlackBrush);
            SelectObject(maskDc, maskBlackPen);
            RoundRect(maskDc, 1, 1, IconSize - 1, IconSize - 1, 9, 9);

            var fontHeight = text.Length switch
            {
                1 => -22,
                2 => -18,
                _ => -13,
            };
            font = CreateFontW(
                fontHeight,
                0,
                0,
                0,
                FontWeightSemibold,
                0,
                0,
                0,
                1,
                0,
                0,
                4,
                0,
                "Segoe UI Semibold");
            if (font == nint.Zero)
                return nint.Zero;

            oldFont = SelectObject(colorDc, font);
            SetBkMode(colorDc, Transparent);
            SetTextColor(colorDc, foreground);
            var textRect = new Rect { Left = 0, Top = -1, Right = IconSize, Bottom = IconSize - 1 };
            DrawTextW(
                colorDc,
                text,
                text.Length,
                ref textRect,
                DtCenter | DtVCenter | DtSingleLine | DtNoPrefix);

            var iconInfo = new IconInfo
            {
                IsIcon = true,
                ColorBitmap = colorBitmap,
                MaskBitmap = maskBitmap,
            };
            return CreateIconIndirect(ref iconInfo);
        }
        finally
        {
            if (oldFont != nint.Zero) SelectObject(colorDc, oldFont);
            if (oldColorBrush != nint.Zero) SelectObject(colorDc, oldColorBrush);
            if (oldColorPen != nint.Zero) SelectObject(colorDc, oldColorPen);
            if (oldMaskBrush != nint.Zero) SelectObject(maskDc, oldMaskBrush);
            if (oldMaskPen != nint.Zero) SelectObject(maskDc, oldMaskPen);
            if (oldColorBitmap != nint.Zero) SelectObject(colorDc, oldColorBitmap);
            if (oldMaskBitmap != nint.Zero) SelectObject(maskDc, oldMaskBitmap);

            DeleteIfNotZero(font);
            DeleteIfNotZero(backgroundBrush);
            DeleteIfNotZero(backgroundPen);
            DeleteIfNotZero(clearBrush);
            DeleteIfNotZero(clearPen);
            DeleteIfNotZero(maskWhiteBrush);
            DeleteIfNotZero(maskWhitePen);
            DeleteIfNotZero(maskBlackBrush);
            DeleteIfNotZero(maskBlackPen);
            DeleteIfNotZero(colorBitmap);
            DeleteIfNotZero(maskBitmap);
            if (colorDc != nint.Zero) DeleteDC(colorDc);
            if (maskDc != nint.Zero) DeleteDC(maskDc);
            ReleaseDC(nint.Zero, screenDc);
        }
    }

    private static void DeleteIfNotZero(nint obj)
    {
        if (obj != nint.Zero)
            DeleteObject(obj);
    }

    private static uint Rgb(byte red, byte green, byte blue) =>
        red | ((uint)green << 8) | ((uint)blue << 16);

    private static bool HasLightBackground(uint color)
    {
        var red = color & 0xFF;
        var green = (color >> 8) & 0xFF;
        var blue = (color >> 16) & 0xFF;
        return red * 299 + green * 587 + blue * 114 >= 160000;
    }
}
