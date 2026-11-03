using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FindTheNeedleEditor;

/// <summary>Dark window caption: immersive dark mode (Windows 10 20H1+) and, on Windows 11, caption/border/text colours.</summary>
public static class DarkTitleBar
{
    const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWA_CAPTION_COLOR = 35;
    const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    static int Rgb(byte r, byte g, byte b) => r | (g << 8) | (b << 16); // COLORREF = 0x00BBGGRR

    public static void Apply(Window w)
    {
        void Set()
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, 4);
            int caption = Rgb(0x13, 0x1A, 0x1E), border = Rgb(0x27, 0x35, 0x3B), text = Rgb(0xE7, 0xE9, 0xF0);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, 4);
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, 4);
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, 4);
        }
        if (new WindowInteropHelper(w).Handle != IntPtr.Zero) Set();
        else w.SourceInitialized += (_, _) => Set();
    }
}
