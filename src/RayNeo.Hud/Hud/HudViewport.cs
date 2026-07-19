// -----------------------------------------------------------------------------
// HudViewport.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Hud;

/// <summary>
/// Maps head-relative angles (degrees) onto canvas coordinates for the current
/// window size and the glasses' field of view. All values are WPF
/// device-independent pixels, so the mapping is DPI-correct on any monitor.
/// </summary>
public readonly struct HudViewport
{
    /// <summary>Creates a viewport for a canvas of the given size and FOV.</summary>
    public HudViewport(double width, double height, double horizontalFovDeg, double verticalFovDeg)
    {
        Width = width;
        Height = height;
        HorizontalFovDeg = horizontalFovDeg;
        VerticalFovDeg = verticalFovDeg;
    }

    /// <summary>Canvas width in DIPs.</summary>
    public double Width { get; }

    /// <summary>Canvas height in DIPs.</summary>
    public double Height { get; }

    /// <summary>Horizontal field of view in degrees.</summary>
    public double HorizontalFovDeg { get; }

    /// <summary>Vertical field of view in degrees.</summary>
    public double VerticalFovDeg { get; }

    /// <summary>Horizontal centre of the canvas.</summary>
    public double CenterX => Width / 2.0;

    /// <summary>Vertical centre of the canvas.</summary>
    public double CenterY => Height / 2.0;

    /// <summary>Horizontal pixels per degree of yaw.</summary>
    public double PixelsPerDegreeX => Width / HorizontalFovDeg;

    /// <summary>Vertical pixels per degree of pitch.</summary>
    public double PixelsPerDegreeY => Height / VerticalFovDeg;

    /// <summary>True once the canvas has a real (laid-out) size.</summary>
    public bool IsValid => Width > 0 && Height > 0;
}
