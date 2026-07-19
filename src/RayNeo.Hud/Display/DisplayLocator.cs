// -----------------------------------------------------------------------------
// DisplayLocator.cs
// Author: Kurt Mitchell
//
// Chooses which monitor the HUD renders onto. Preference order:
//   1. An explicit --display N argument.
//   2. A secondary monitor matching the glasses' native resolution.
//   3. Any secondary monitor (best guess) — with a warning.
//   4. The primary monitor — with a warning (glasses likely not connected).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinyte.RayNeo.Hud.Display;

/// <summary>The chosen display plus an optional warning to surface on the HUD.</summary>
public sealed record DisplaySelection(DisplayInfo Display, string? Warning);

/// <summary>Resolves the target display for the overlay window.</summary>
public static class DisplayLocator
{
    // The RayNeo Air 4 Pro presents to Windows as a 1920x1080 secondary display.
    private const int GlassesWidth = 1920;
    private const int GlassesHeight = 1080;

    /// <summary>Picks the display to render on. See file header for the order.</summary>
    public static DisplaySelection Choose(IReadOnlyList<DisplayInfo> displays, int? requestedIndex)
    {
        if (displays.Count == 0)
        {
            throw new InvalidOperationException("No displays were reported by the OS.");
        }

        DisplayInfo primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];

        if (requestedIndex is int idx)
        {
            DisplayInfo? requested = displays.FirstOrDefault(d => d.Index == idx);
            return requested is not null
                ? new DisplaySelection(requested, null)
                : new DisplaySelection(primary, $"--display {idx} not found; using primary display {primary}.");
        }

        DisplayInfo? glasses = displays.FirstOrDefault(
            d => !d.IsPrimary && d.Width == GlassesWidth && d.Height == GlassesHeight);
        if (glasses is not null)
        {
            return new DisplaySelection(glasses, null);
        }

        DisplayInfo? secondary = displays.FirstOrDefault(d => !d.IsPrimary);
        if (secondary is not null)
        {
            return new DisplaySelection(secondary,
                $"Glasses display ({GlassesWidth}x{GlassesHeight}) not found; using secondary display {secondary}.");
        }

        return new DisplaySelection(primary,
            "No secondary display detected; rendering on the primary display. " +
            "Connect the glasses or pass --display N.");
    }
}
