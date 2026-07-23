// -----------------------------------------------------------------------------
// HudThemeSceneBuilder.cs
// Author: Kurt Mitchell
//
// Turns a validated HudTheme into live HUD elements and registers them with the
// compositor. Each manifest element becomes a WPF visual (Image / TextBlock /
// nine-slice panel / crosshair) wrapped in the existing ScreenFixedElement or
// WorldAnchoredElement, so themed content flows through the exact same
// anchoring, FOV clamp/fade, and roll-leveling as the built-in HUD. This is the
// only theme file that touches WPF visuals; all the fallible logic it relies on
// (parsing, binding, slicing) lives in tested, display-free helpers.
//
// Must run on the UI thread (it creates WPF visuals and adds them to the canvas).
// -----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Infinyte.RayNeo.Hud;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>Builds a themed HUD scene onto a <see cref="HudCompositor"/>.</summary>
public sealed class HudThemeSceneBuilder
{
    private const double DefaultFontSize = 18;
    private const double DefaultMargin = 24;
    private const double DefaultCrosshairSize = 84;

    private readonly HudTheme _theme;
    private readonly HudCompositor _compositor;
    private readonly IHeadOrientationProvider _provider;

    /// <summary>Creates a builder for <paramref name="theme"/> over the given compositor and provider.</summary>
    public HudThemeSceneBuilder(HudTheme theme, HudCompositor compositor, IHeadOrientationProvider provider)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _compositor = compositor ?? throw new ArgumentNullException(nameof(compositor));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>Builds every element in the theme and adds it to the compositor.</summary>
    public void Build()
    {
        foreach (HudThemeElement element in _theme.Elements)
        {
            BuildElement(element);
        }
    }

    private void BuildElement(HudThemeElement element)
    {
        string type = element.Type!.Trim().ToLowerInvariant();
        HudAnchorSpec anchor = ResolveAnchor(element, type);

        switch (type)
        {
            case "text":
                BuildText(element, anchor);
                break;
            case "image":
                BuildImage(element, anchor);
                break;
            case "panel":
                BuildPanel(element, anchor);
                break;
            case "crosshair":
                BuildCrosshair(element, anchor);
                break;
        }
    }

    // ---- Element builders ---------------------------------------------------

    private void BuildText(HudThemeElement element, HudAnchorSpec anchor)
    {
        // Text is validated to be screen-anchored (world-locked live text is unsupported).
        TextBlock text = MakeText(element);
        Action<HeadOrientation> onFrame = BindText(text, element.Format!);
        _compositor.Add(new ScreenFixedElement(text, anchor.Screen, Margin(element), onFrame));
    }

    private void BuildImage(HudThemeElement element, HudAnchorSpec anchor)
    {
        Image image = LoadImage(AssetPath(element.Asset!), element.Width, element.Height, Opacity(element), Glow());

        if (anchor.Kind == HudAnchorKind.World)
        {
            _compositor.Add(WorldElement(image, element.Width!.Value, element.Height!.Value, element));
        }
        else
        {
            _compositor.Add(new ScreenFixedElement(image, anchor.Screen, Margin(element)));
        }
    }

    private void BuildPanel(HudThemeElement element, HudAnchorSpec anchor)
    {
        double width = element.Width!.Value;
        double height = element.Height!.Value;

        FrameworkElement background = BuildPanelBackground(element, width, height);

        FrameworkElement visual;
        Action<HeadOrientation>? onFrame = null;
        if (!string.IsNullOrEmpty(element.Format))
        {
            var container = new Grid { Width = width, Height = height, IsHitTestVisible = false };
            container.Children.Add(background);

            TextBlock label = MakeText(element);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MaxWidth = Math.Max(1, width - 8);
            container.Children.Add(label);

            visual = container;
            onFrame = BindText(label, element.Format!);
        }
        else
        {
            visual = background;
        }

        visual.Opacity = Opacity(element);

        if (anchor.Kind == HudAnchorKind.World)
        {
            // A world-locked panel renders its text once (the world element has no
            // per-frame hook); dynamic tokens are captured from the current frame.
            onFrame?.Invoke(_provider.Current);
            _compositor.Add(WorldElement(visual, width, height, element));
        }
        else
        {
            _compositor.Add(new ScreenFixedElement(visual, anchor.Screen, Margin(element), onFrame));
        }
    }

    private void BuildCrosshair(HudThemeElement element, HudAnchorSpec anchor)
    {
        double width = element.Width ?? DefaultCrosshairSize;
        double height = element.Height ?? DefaultCrosshairSize;

        FrameworkElement visual = string.IsNullOrEmpty(element.Asset)
            ? BuildVectorCrosshair(ResolveBrush(element), width, height)
            : LoadImage(AssetPath(element.Asset!), width, height, Opacity(element), Glow());
        visual.Opacity = Opacity(element);

        if (anchor.Kind == HudAnchorKind.World)
        {
            _compositor.Add(new WorldAnchoredElement(
                visual, width, height,
                (float)(element.YawDeg ?? 0.0), (float)(element.PitchDeg ?? 0.0),
                element.LevelWithHorizon ?? true, element.AnchorToFirstFrame ?? true));
        }
        else
        {
            _compositor.Add(new ScreenFixedElement(visual, anchor.Screen, Margin(element)));
        }
    }

    private FrameworkElement BuildPanelBackground(HudThemeElement element, double width, double height)
    {
        BitmapImage bitmap = LoadBitmap(AssetPath(element.Asset!));
        if (element.Slice is not null)
        {
            try
            {
                return NineSlicePanel.Create(bitmap, element.Slice, width, height);
            }
            catch (ArgumentException ex)
            {
                throw new HudThemeException(
                    $"theme '{_theme.Name}': panel asset '{element.Asset}' cannot be nine-sliced: {ex.Message}", ex);
            }
        }

        return new Image
        {
            Source = bitmap,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
        };
    }

    // ---- Wrapping + binding -------------------------------------------------

    private WorldAnchoredElement WorldElement(FrameworkElement visual, double width, double height, HudThemeElement element) =>
        new(visual, width, height,
            (float)(element.YawDeg ?? 0.0), (float)(element.PitchDeg ?? 0.0),
            element.LevelWithHorizon ?? false, element.AnchorToFirstFrame ?? false);

    private Action<HeadOrientation> BindText(TextBlock target, string format) => orientation =>
    {
        var values = new HudBindingValues(
            orientation.PitchDegrees, orientation.YawDegrees, orientation.RollDegrees,
            orientation.TemperatureCelsius, _provider.StatusText, ConnectionWord(orientation), DateTime.Now);
        target.Text = HudBinding.Format(format, values);
    };

    private static string ConnectionWord(HeadOrientation orientation) =>
        orientation.IsLive ? "connected" : orientation.IsSimulated ? "simulated" : "disconnected";

    // ---- Visual factories ---------------------------------------------------

    private TextBlock MakeText(HudThemeElement element)
    {
        var text = new TextBlock
        {
            FontFamily = FontResolver.Resolve(element.Font ?? _theme.Defaults.Font, _theme.Folder),
            FontSize = element.FontSize ?? _theme.Defaults.FontSize ?? DefaultFontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush(element),
            TextAlignment = ParseAlign(element.Align),
            IsHitTestVisible = false,
        };
        if (Glow())
        {
            text.Effect = MakeGlow();
        }
        return text;
    }

    private Image LoadImage(string path, double? width, double? height, double opacity, bool glow)
    {
        var image = new Image
        {
            Source = LoadBitmap(path),
            Stretch = Stretch.Fill,
            Opacity = opacity,
            IsHitTestVisible = false,
        };
        if (width is > 0)
        {
            image.Width = width.Value;
        }
        if (height is > 0)
        {
            image.Height = height.Value;
        }
        if (glow)
        {
            image.Effect = MakeGlow();
        }
        return image;
    }

    // A scalable vector crosshair (four ticks + center ring) matching the
    // built-in HUD, wrapped in a Viewbox so it renders at any requested size.
    private static FrameworkElement BuildVectorCrosshair(Brush brush, double width, double height)
    {
        var canvas = new Canvas { Width = 84, Height = 84, IsHitTestVisible = false };

        void AddLine(double x1, double y1, double x2, double y2) =>
            canvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = brush, StrokeThickness = 2.5,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            });

        AddLine(42, 8, 42, 30);
        AddLine(42, 54, 42, 76);
        AddLine(8, 42, 30, 42);
        AddLine(54, 42, 76, 42);

        var ring = new Ellipse { Width = 16, Height = 16, Stroke = brush, StrokeThickness = 2.5 };
        Canvas.SetLeft(ring, 34);
        Canvas.SetTop(ring, 34);
        canvas.Children.Add(ring);

        canvas.Effect = MakeGlow();

        return new Viewbox { Width = width, Height = height, Child = canvas, IsHitTestVisible = false };
    }

    private static DropShadowEffect MakeGlow() =>
        new() { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 };

    // ---- Small resolvers ----------------------------------------------------

    private HudAnchorSpec ResolveAnchor(HudThemeElement element, string type)
    {
        if (element.Anchor is not null && ThemeAnchors.TryParse(element.Anchor, out HudAnchorSpec parsed))
        {
            return parsed;
        }
        // Defaults: a crosshair is world-locked; everything else pins to the top-left.
        return type == "crosshair" ? HudAnchorSpec.World : new HudAnchorSpec(ScreenAnchor.TopLeft);
    }

    private Brush ResolveBrush(HudThemeElement element) =>
        ParseBrush(element.Color ?? _theme.Defaults.Color, Brushes.White);

    private bool Glow() => _theme.Defaults.Glow ?? true;

    private static double Margin(HudThemeElement element) => element.Margin ?? DefaultMargin;

    private static double Opacity(HudThemeElement element) =>
        element.Opacity is { } o ? Math.Clamp(o, 0.0, 1.0) : 1.0;

    private string AssetPath(string asset) => Path.Combine(_theme.Folder, asset);

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // fully load now; do not lock the file
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush ParseBrush(string? hex, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }
        try
        {
            object? converted = ColorConverter.ConvertFromString(hex);
            if (converted is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
        catch (Exception)
        {
            // Fall through to the fallback on an unparseable color.
        }
        return fallback;
    }

    private static TextAlignment ParseAlign(string? align) =>
        (align?.Trim().ToLowerInvariant()) switch
        {
            "center" or "centre" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            "justify" => TextAlignment.Justify,
            _ => TextAlignment.Left,
        };
}
