// -----------------------------------------------------------------------------
// ThemeAnchors.cs
// Author: Kurt Mitchell
//
// Parses a manifest anchor string into either a screen corner/edge
// (ScreenAnchor) or a world-locked marker. Pure and case/separator tolerant so
// authors can write "top-left", "TopLeft", or "top_left" interchangeably.
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo.Hud;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>Whether an element is pinned to the screen or locked to a world direction.</summary>
public enum HudAnchorKind
{
    /// <summary>Pinned to a screen corner or edge.</summary>
    Screen,

    /// <summary>Locked to a direction in the room.</summary>
    World,
}

/// <summary>A parsed anchor: a kind plus, for screen anchors, the corner/edge.</summary>
public readonly struct HudAnchorSpec
{
    /// <summary>Creates a screen anchor spec.</summary>
    public HudAnchorSpec(ScreenAnchor screen)
    {
        Kind = HudAnchorKind.Screen;
        Screen = screen;
    }

    private HudAnchorSpec(HudAnchorKind kind)
    {
        Kind = kind;
        Screen = ScreenAnchor.TopLeft;
    }

    /// <summary>A world-locked anchor.</summary>
    public static HudAnchorSpec World { get; } = new(HudAnchorKind.World);

    /// <summary>Screen or world.</summary>
    public HudAnchorKind Kind { get; }

    /// <summary>The screen corner/edge (meaningful only when <see cref="Kind"/> is Screen).</summary>
    public ScreenAnchor Screen { get; }
}

/// <summary>Parses manifest anchor strings.</summary>
public static class ThemeAnchors
{
    /// <summary>
    /// Attempts to parse <paramref name="text"/> into an anchor spec. Accepts
    /// "world" and any casing/separator variant of the six screen anchors
    /// (e.g. "top-left", "TopLeft", "top_left", "bottom center").
    /// </summary>
    public static bool TryParse(string? text, out HudAnchorSpec anchor)
    {
        anchor = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string key = Normalize(text);
        if (key == "world")
        {
            anchor = HudAnchorSpec.World;
            return true;
        }

        switch (key)
        {
            case "topleft":
                anchor = new HudAnchorSpec(ScreenAnchor.TopLeft);
                return true;
            case "topcenter":
            case "topcentre":
                anchor = new HudAnchorSpec(ScreenAnchor.TopCenter);
                return true;
            case "topright":
                anchor = new HudAnchorSpec(ScreenAnchor.TopRight);
                return true;
            case "bottomleft":
                anchor = new HudAnchorSpec(ScreenAnchor.BottomLeft);
                return true;
            case "bottomcenter":
            case "bottomcentre":
                anchor = new HudAnchorSpec(ScreenAnchor.BottomCenter);
                return true;
            case "bottomright":
                anchor = new HudAnchorSpec(ScreenAnchor.BottomRight);
                return true;
            default:
                return false;
        }
    }

    // Lowercase and strip spaces, hyphens, and underscores so separator style
    // never matters to a theme author.
    private static string Normalize(string text)
    {
        Span<char> buffer = stackalloc char[text.Length];
        int n = 0;
        foreach (char c in text)
        {
            if (c is ' ' or '-' or '_')
            {
                continue;
            }
            buffer[n++] = char.ToLowerInvariant(c);
        }
        return new string(buffer[..n]);
    }
}
