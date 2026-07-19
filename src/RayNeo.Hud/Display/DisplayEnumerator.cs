// -----------------------------------------------------------------------------
// DisplayEnumerator.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Infinyte.RayNeo.Hud.Interop;

namespace Infinyte.RayNeo.Hud.Display;

/// <summary>Enumerates the attached monitors via the Win32 monitor API.</summary>
public static class DisplayEnumerator
{
    /// <summary>Returns every attached monitor, in enumeration order.</summary>
    public static IReadOnlyList<DisplayInfo> All()
    {
        var displays = new List<DisplayInfo>();
        int index = 0;

        // Keep the delegate rooted for the duration of the call.
        NativeMethods.MonitorEnumProc callback =
            (IntPtr monitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                var info = new NativeMethods.MONITORINFO
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>(),
                };
                if (NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    bool primary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                    NativeMethods.RECT b = info.rcMonitor;
                    displays.Add(new DisplayInfo(index++, b.Left, b.Top, b.Width, b.Height, primary));
                }
                return true; // continue enumeration
            };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        return displays;
    }
}
