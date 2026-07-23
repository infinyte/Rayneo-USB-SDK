// -----------------------------------------------------------------------------
// HudThemeManifest.cs
// Author: Kurt Mitchell
//
// Data-transfer objects for a theme.json manifest. These are the on-disk shape
// of a HUD theme: a set of defaults plus an ordered list of elements. They are
// pure POCOs with no WPF dependency so the loader and its tests can parse and
// validate a theme without a display.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>The root object of a <c>theme.json</c> manifest.</summary>
public sealed class HudThemeManifest
{
    /// <summary>Human-readable theme name (required).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Optional theme author.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>Optional theme version string.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Optional free-text description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Style values inherited by elements that do not override them.</summary>
    [JsonPropertyName("defaults")]
    public HudThemeDefaults? Defaults { get; set; }

    /// <summary>The ordered list of HUD elements the theme draws (required, non-empty).</summary>
    [JsonPropertyName("elements")]
    public List<HudThemeElement>? Elements { get; set; }
}

/// <summary>Theme-wide style defaults applied to every element unless overridden.</summary>
public sealed class HudThemeDefaults
{
    /// <summary>Default font: a system family name, or a bundled <c>.ttf</c>/<c>.otf</c> file.</summary>
    [JsonPropertyName("font")]
    public string? Font { get; set; }

    /// <summary>Default text size in DIPs.</summary>
    [JsonPropertyName("fontSize")]
    public double? FontSize { get; set; }

    /// <summary>Default foreground color as <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>Whether text and images get a soft dark glow so they read on the see-through display.</summary>
    [JsonPropertyName("glow")]
    public bool? Glow { get; set; }
}

/// <summary>A single element in a theme: text, image, panel, or crosshair.</summary>
public sealed class HudThemeElement
{
    /// <summary>Element kind: <c>text</c>, <c>image</c>, <c>panel</c>, or <c>crosshair</c> (required).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Anchor: a screen corner/edge (<c>top-left</c> … <c>bottom-right</c>) or
    /// <c>world</c> for a direction-locked element.
    /// </summary>
    [JsonPropertyName("anchor")]
    public string? Anchor { get; set; }

    /// <summary>Screen-anchor inset from the edge in DIPs.</summary>
    [JsonPropertyName("margin")]
    public double? Margin { get; set; }

    /// <summary>
    /// Text/panel content, with binding tokens such as <c>{pitch:F1}</c> or
    /// <c>{clock:HH:mm:ss}</c>. See <see cref="HudBinding"/> for the vocabulary.
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>Text size override in DIPs.</summary>
    [JsonPropertyName("fontSize")]
    public double? FontSize { get; set; }

    /// <summary>Foreground color override.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>Font override (system family name or bundled <c>.ttf</c>/<c>.otf</c>).</summary>
    [JsonPropertyName("font")]
    public string? Font { get; set; }

    /// <summary>Text alignment for multi-line/centered content: <c>left</c>, <c>center</c>, or <c>right</c>.</summary>
    [JsonPropertyName("align")]
    public string? Align { get; set; }

    /// <summary>Asset file name (relative to the theme folder) for image/panel/crosshair elements.</summary>
    [JsonPropertyName("asset")]
    public string? Asset { get; set; }

    /// <summary>Element width in DIPs (required for panels and world-anchored elements).</summary>
    [JsonPropertyName("width")]
    public double? Width { get; set; }

    /// <summary>Element height in DIPs (required for panels and world-anchored elements).</summary>
    [JsonPropertyName("height")]
    public double? Height { get; set; }

    /// <summary>Overall element opacity (0..1); defaults to fully opaque.</summary>
    [JsonPropertyName("opacity")]
    public double? Opacity { get; set; }

    /// <summary>Nine-slice margins for a stretchable panel background.</summary>
    [JsonPropertyName("slice")]
    public HudThemeSlice? Slice { get; set; }

    /// <summary>World yaw (degrees) a world-anchored element is locked to.</summary>
    [JsonPropertyName("yawDeg")]
    public double? YawDeg { get; set; }

    /// <summary>World pitch (degrees) a world-anchored element is locked to.</summary>
    [JsonPropertyName("pitchDeg")]
    public double? PitchDeg { get; set; }

    /// <summary>When true, a world element counter-rotates by head roll to stay level with the horizon.</summary>
    [JsonPropertyName("levelWithHorizon")]
    public bool? LevelWithHorizon { get; set; }

    /// <summary>When true, a world element captures its anchor from the first frame (current gaze).</summary>
    [JsonPropertyName("anchorToFirstFrame")]
    public bool? AnchorToFirstFrame { get; set; }
}

/// <summary>Nine-slice margins (in source pixels) for a stretchable panel image.</summary>
public sealed class HudThemeSlice
{
    /// <summary>Left margin in source pixels.</summary>
    [JsonPropertyName("left")]
    public double Left { get; set; }

    /// <summary>Top margin in source pixels.</summary>
    [JsonPropertyName("top")]
    public double Top { get; set; }

    /// <summary>Right margin in source pixels.</summary>
    [JsonPropertyName("right")]
    public double Right { get; set; }

    /// <summary>Bottom margin in source pixels.</summary>
    [JsonPropertyName("bottom")]
    public double Bottom { get; set; }
}
