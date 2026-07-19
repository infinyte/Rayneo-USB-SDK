// -----------------------------------------------------------------------------
// NativeMethods.cs
// Author: Kurt Mitchell
//
// P/Invoke surface for the HUD overlay: monitor enumeration and the window
// styling needed for a click-through, layered, always-on-top overlay.
// -----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Infinyte.RayNeo.Hud.Interop;

internal static class NativeMethods
{
    // ---- Window styles ------------------------------------------------------
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TRANSPARENT = 0x00000020; // click-through (input passes beneath)
    public const long WS_EX_LAYERED = 0x00080000;     // per-pixel alpha compositing
    public const long WS_EX_TOOLWINDOW = 0x00000080;  // keep out of the alt-tab list
    public const long WS_EX_NOACTIVATE = 0x08000000;  // never steal foreground/focus

    // ---- SetWindowPos -------------------------------------------------------
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // ---- Monitor enumeration ------------------------------------------------
    public const uint MONITORINFOF_PRIMARY = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprc, IntPtr data);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    // ---- Window styling / placement ----------------------------------------
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
