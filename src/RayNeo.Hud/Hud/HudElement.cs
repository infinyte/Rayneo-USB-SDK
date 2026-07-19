// -----------------------------------------------------------------------------
// HudElement.cs
// Author: Kurt Mitchell
//
// The two HUD element modes:
//   ScreenFixedElement  — static chrome pinned to an edge/corner of the display.
//   WorldAnchoredElement — locked to a direction in the room; slides opposite the
//                          gaze, clamping and fading near the field-of-view edge.
// -----------------------------------------------------------------------------

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Infinyte.RayNeo.Hud;

/// <summary>A single visual placed on the HUD canvas each frame.</summary>
public abstract class HudElement
{
    /// <summary>Creates an element wrapping the given visual.</summary>
    protected HudElement(FrameworkElement visual) => Visual = visual;

    /// <summary>The WPF visual positioned on the canvas.</summary>
    public FrameworkElement Visual { get; }

    /// <summary>Repositions (and restyles) the visual for the current orientation.</summary>
    public abstract void Arrange(HeadOrientation orientation, in HudViewport viewport);
}

/// <summary>Corner/edge the chrome is pinned to.</summary>
public enum ScreenAnchor
{
    /// <summary>Top-left corner.</summary>
    TopLeft,
    /// <summary>Top edge, centred.</summary>
    TopCenter,
    /// <summary>Top-right corner.</summary>
    TopRight,
    /// <summary>Bottom-left corner.</summary>
    BottomLeft,
    /// <summary>Bottom edge, centred.</summary>
    BottomCenter,
    /// <summary>Bottom-right corner.</summary>
    BottomRight,
}

/// <summary>
/// Static HUD chrome (clock, connection status, debug readout) pinned to a
/// corner or edge. Position tracks the window size; an optional per-frame
/// callback refreshes dynamic text.
/// </summary>
public sealed class ScreenFixedElement : HudElement
{
    private readonly ScreenAnchor _anchor;
    private readonly double _margin;
    private readonly Action<HeadOrientation>? _onFrame;

    /// <summary>Pins <paramref name="visual"/> to <paramref name="anchor"/>.</summary>
    public ScreenFixedElement(
        FrameworkElement visual, ScreenAnchor anchor, double margin = 24, Action<HeadOrientation>? onFrame = null)
        : base(visual)
    {
        _anchor = anchor;
        _margin = margin;
        _onFrame = onFrame;
    }

    /// <inheritdoc/>
    public override void Arrange(HeadOrientation orientation, in HudViewport viewport)
    {
        _onFrame?.Invoke(orientation);

        // Measure so we can right- and bottom-align against the current text.
        Visual.Measure(new Size(viewport.Width, viewport.Height));
        double w = Visual.DesiredSize.Width;
        double h = Visual.DesiredSize.Height;

        double left = _anchor switch
        {
            ScreenAnchor.TopLeft or ScreenAnchor.BottomLeft => _margin,
            ScreenAnchor.TopCenter or ScreenAnchor.BottomCenter => (viewport.Width - w) / 2.0,
            _ => viewport.Width - w - _margin,
        };
        double top = _anchor switch
        {
            ScreenAnchor.TopLeft or ScreenAnchor.TopCenter or ScreenAnchor.TopRight => _margin,
            _ => viewport.Height - h - _margin,
        };

        Canvas.SetLeft(Visual, left);
        Canvas.SetTop(Visual, top);
    }
}

/// <summary>
/// Holds a fixed direction in the room. As the head turns the element slides
/// opposite the gaze so it appears world-locked, and it fades to transparent as
/// it nears the FOV edge (clamped to the edge rather than popping off-screen).
/// </summary>
public sealed class WorldAnchoredElement : HudElement
{
    // Full opacity out to this fraction of the half-FOV, then a linear fade to
    // zero at the edge.
    private const double FadeStart = 0.70;

    private readonly double _halfWidth;
    private readonly double _halfHeight;
    private readonly RotateTransform? _rollTransform;
    private bool _anchorToFirstFrame;

    /// <summary>World yaw (degrees) this element is anchored to.</summary>
    public float AnchorYawDegrees { get; set; }

    /// <summary>World pitch (degrees) this element is anchored to.</summary>
    public float AnchorPitchDegrees { get; set; }

    /// <summary>Anchors <paramref name="visual"/> to a world direction.</summary>
    /// <param name="levelWithHorizon">
    /// When true, counter-rotates the visual by the head roll so it stays level
    /// with the room (useful for the debug crosshair).
    /// </param>
    /// <param name="anchorToFirstFrame">
    /// When true, the anchor is captured from the first orientation seen, so the
    /// element starts centred on the current gaze regardless of head pose or yaw
    /// drift. It then holds that world direction as the head moves.
    /// </param>
    public WorldAnchoredElement(
        FrameworkElement visual, double width, double height,
        float anchorYawDeg, float anchorPitchDeg, bool levelWithHorizon = false, bool anchorToFirstFrame = false)
        : base(visual)
    {
        _halfWidth = width / 2.0;
        _halfHeight = height / 2.0;
        AnchorYawDegrees = anchorYawDeg;
        AnchorPitchDegrees = anchorPitchDeg;
        _anchorToFirstFrame = anchorToFirstFrame;

        if (levelWithHorizon)
        {
            _rollTransform = new RotateTransform(0);
            visual.RenderTransformOrigin = new Point(0.5, 0.5);
            visual.RenderTransform = _rollTransform;
        }
    }

    /// <inheritdoc/>
    public override void Arrange(HeadOrientation orientation, in HudViewport viewport)
    {
        if (_anchorToFirstFrame)
        {
            AnchorYawDegrees = orientation.YawDegrees;
            AnchorPitchDegrees = orientation.PitchDegrees;
            _anchorToFirstFrame = false; // captured once; hold it thereafter
        }

        double deltaYaw = orientation.YawDegrees - AnchorYawDegrees;
        double deltaPitch = orientation.PitchDegrees - AnchorPitchDegrees;

        // World-lock signs verified live on the glasses: turning the head slides
        // the anchored point opposite the gaze horizontally, and looking up/down
        // slides it down/up. The yaw term is added (not subtracted) to match the
        // device's yaw polarity, confirmed by the on-glasses anchoring check.
        double x = viewport.CenterX + deltaYaw * viewport.PixelsPerDegreeX;
        double y = viewport.CenterY + deltaPitch * viewport.PixelsPerDegreeY;

        // Fade by angular eccentricity from the gaze centre.
        double eccentricity = Math.Max(
            Math.Abs(deltaYaw) / (viewport.HorizontalFovDeg / 2.0),
            Math.Abs(deltaPitch) / (viewport.VerticalFovDeg / 2.0));
        Visual.Opacity = eccentricity <= FadeStart
            ? 1.0
            : Math.Clamp(1.0 - (eccentricity - FadeStart) / (1.0 - FadeStart), 0.0, 1.0);

        // Clamp so the element rides the edge while faded instead of vanishing.
        x = Math.Clamp(x, _halfWidth, Math.Max(_halfWidth, viewport.Width - _halfWidth));
        y = Math.Clamp(y, _halfHeight, Math.Max(_halfHeight, viewport.Height - _halfHeight));

        Canvas.SetLeft(Visual, x - _halfWidth);
        Canvas.SetTop(Visual, y - _halfHeight);

        if (_rollTransform is not null)
        {
            _rollTransform.Angle = -orientation.RollDegrees; // keep the crosshair level
        }
    }
}
