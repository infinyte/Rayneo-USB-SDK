// -----------------------------------------------------------------------------
// PinSurface.cs
// Author: Kurt Mitchell
//
// World-anchored sticky notes ("pin milk to my left"). Pins are placed at a
// yaw/pitch offset from the wearer's current gaze and then hold that world
// direction via WorldAnchoredElement. Tool calls arrive on the assistant
// loop's thread, so every canvas mutation is marshalled to the UI thread.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>Manages the world-anchored note pins on the HUD.</summary>
public sealed class PinSurface
{
    private const double PinWidth = 260;
    private const double PinHeight = 90;

    // Angular offsets from the current gaze for each spoken direction. The
    // lateral sign follows the device's yaw polarity as used by
    // WorldAnchoredElement (verified on-glass); flip the sign here if a live
    // smoke test shows left/right mirrored.
    private const float SideYawOffsetDeg = 25f;
    private const float VerticalPitchOffsetDeg = 12f;

    private readonly Dispatcher _dispatcher;
    private readonly HudCompositor _compositor;
    private readonly IHeadOrientationProvider _provider;
    private readonly List<(string Text, string Direction, WorldAnchoredElement Element)> _pins = new();
    private readonly object _gate = new();

    /// <summary>Creates the surface over the HUD compositor.</summary>
    public PinSurface(Dispatcher dispatcher, HudCompositor compositor, IHeadOrientationProvider provider)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _compositor = compositor ?? throw new ArgumentNullException(nameof(compositor));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>Valid spoken directions for a pin.</summary>
    public static IReadOnlyList<string> Directions { get; } =
        new[] { "ahead", "left", "right", "up", "down" };

    /// <summary>Pins <paramref name="text"/> at <paramref name="direction"/> relative to the current gaze.</summary>
    /// <returns>A confirmation string for the model.</returns>
    public string AddPin(string text, string direction)
    {
        direction = direction.Trim().ToLowerInvariant();
        if (!Directions.Contains(direction))
        {
            return $"Unknown direction '{direction}'. Use one of: {string.Join(", ", Directions)}.";
        }

        HeadOrientation gaze = _provider.Current;
        (float yawOffset, float pitchOffset) = direction switch
        {
            "left" => (-SideYawOffsetDeg, 0f),
            "right" => (SideYawOffsetDeg, 0f),
            "up" => (0f, VerticalPitchOffsetDeg),
            "down" => (0f, -VerticalPitchOffsetDeg),
            _ => (0f, 0f),
        };

        _dispatcher.Invoke(() =>
        {
            var element = new WorldAnchoredElement(
                BuildPinVisual(text), PinWidth, PinHeight,
                gaze.YawDegrees + yawOffset, gaze.PitchDegrees + pitchOffset);
            lock (_gate)
            {
                _pins.Add((text, direction, element));
            }
            _compositor.Add(element);
        });
        return $"Pinned '{text}' {(direction == "ahead" ? "straight ahead" : direction)}.";
    }

    /// <summary>Describes the current pins for the model.</summary>
    public string ListPins()
    {
        lock (_gate)
        {
            if (_pins.Count == 0)
            {
                return "No pins.";
            }
            var sb = new StringBuilder("Pins: ");
            for (int i = 0; i < _pins.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("; ");
                }
                sb.Append($"'{_pins[i].Text}' ({_pins[i].Direction})");
            }
            sb.Append('.');
            return sb.ToString();
        }
    }

    /// <summary>Removes every pin from the HUD.</summary>
    public string ClearPins()
    {
        WorldAnchoredElement[] elements;
        int count;
        lock (_gate)
        {
            count = _pins.Count;
            elements = _pins.Select(p => p.Element).ToArray();
            _pins.Clear();
        }
        if (count == 0)
        {
            return "There were no pins to clear.";
        }
        _dispatcher.Invoke(() =>
        {
            foreach (WorldAnchoredElement element in elements)
            {
                _compositor.Remove(element);
            }
        });
        return count == 1 ? "Cleared 1 pin." : $"Cleared {count} pins.";
    }

    // A translucent dark card with cyan-accented text, sized to stay readable
    // on the see-through display without occluding the world behind it.
    private static FrameworkElement BuildPinVisual(string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = PinWidth - 24,
        };
        return new Border
        {
            Width = PinWidth,
            Height = PinHeight,
            CornerRadius = new CornerRadius(10),
            BorderBrush = Brushes.Cyan,
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromArgb(150, 8, 24, 32)),
            Child = textBlock,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 },
        };
    }
}
