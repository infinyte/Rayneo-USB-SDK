// -----------------------------------------------------------------------------
// MainWindow.xaml.cs
// Author: Kurt Mitchell
//
// The overlay window: borderless, transparent, always-on-top, click-through,
// placed full-screen on the target monitor. Builds the debug HUD scene and
// starts the compositor.
// -----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Infinyte.RayNeo.Hud.Display;
using Infinyte.RayNeo.Hud.Interop;

namespace Infinyte.RayNeo.Hud;

/// <summary>The full-screen HUD overlay window.</summary>
public partial class MainWindow : Window
{
    private static readonly Brush ChromeBrush = Brushes.White;
    private static readonly Brush AccentBrush = Brushes.Cyan;
    private static readonly Brush WarningBrush = Brushes.Gold;

    private readonly DisplayInfo _target;
    private readonly IHeadOrientationProvider _provider;
    private readonly string? _warning;
    private HudCompositor? _compositor;

    /// <summary>Creates the overlay for a specific display and orientation source.</summary>
    public MainWindow(DisplayInfo target, IHeadOrientationProvider provider, string? warning)
    {
        InitializeComponent();
        _target = target;
        _provider = provider;
        _warning = warning;

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _compositor?.Stop();
            _provider.Dispose();
        };
    }

    /// <summary>Applies the click-through styles and places the window on the target monitor.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        // Layered + transparent (click-through), hidden from alt-tab, never activates.
        nint exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= (nint)(NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        // Place the frame using the monitor's physical pixel bounds, bypassing
        // WPF's DIP scaling so placement is exact under mixed-DPI setups.
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            _target.Left, _target.Top, _target.Width, _target.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _compositor = new HudCompositor(Root, _provider);
        BuildScene(_compositor);
        _provider.Start();
        _compositor.Start();
    }

    // ---- Scene --------------------------------------------------------------

    private void BuildScene(HudCompositor compositor)
    {
        // ScreenFixed chrome: clock (top-centre) and connection status (top-left).
        TextBlock clock = MakeText(28, FontWeights.SemiBold, ChromeBrush);
        compositor.Add(new ScreenFixedElement(clock, ScreenAnchor.TopCenter, margin: 20,
            onFrame: _ => clock.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));

        TextBlock status = MakeText(18, FontWeights.SemiBold, AccentBrush);
        compositor.Add(new ScreenFixedElement(status, ScreenAnchor.TopLeft, margin: 24,
            onFrame: o => status.Text = o.IsLive
                ? $"● {_provider.StatusText}    {o.TemperatureCelsius:F1}°C"
                : $"● {_provider.StatusText}"));

        // ScreenFixed debug readout: live pitch / yaw / roll (bottom-centre).
        TextBlock readout = MakeText(20, FontWeights.SemiBold, ChromeBrush);
        readout.TextAlignment = TextAlignment.Center;
        compositor.Add(new ScreenFixedElement(readout, ScreenAnchor.BottomCenter, margin: 28,
            onFrame: o => readout.Text =
                $"pitch {o.PitchDegrees,6:F1}°     yaw {o.YawDegrees,6:F1}°     roll {o.RollDegrees,6:F1}°"));

        // WorldAnchored debug crosshair, locked straight ahead and kept level
        // with the horizon as the head rolls.
        FrameworkElement crosshair = BuildCrosshair();
        compositor.Add(new WorldAnchoredElement(
            crosshair, width: 84, height: 84, anchorYawDeg: 0f, anchorPitchDeg: 0f,
            levelWithHorizon: true, anchorToFirstFrame: true));

        // Surface any startup warning (no glasses / display fallback).
        if (_warning is not null)
        {
            TextBlock warn = MakeText(15, FontWeights.Normal, WarningBrush);
            warn.MaxWidth = 560;
            warn.TextWrapping = TextWrapping.Wrap;
            warn.Text = "⚠ " + _warning;
            compositor.Add(new ScreenFixedElement(warn, ScreenAnchor.BottomLeft, margin: 24));
        }
    }

    // A cyan crosshair: four ticks around a centre ring, with a soft glow so it
    // reads against the see-through background of the glasses.
    private static FrameworkElement BuildCrosshair()
    {
        var canvas = new Canvas { Width = 84, Height = 84, IsHitTestVisible = false };

        void AddLine(double x1, double y1, double x2, double y2) =>
            canvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = AccentBrush, StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            });

        AddLine(42, 8, 42, 30);   // top
        AddLine(42, 54, 42, 76);  // bottom
        AddLine(8, 42, 30, 42);   // left
        AddLine(54, 42, 76, 42);  // right

        var ring = new Ellipse
        {
            Width = 16, Height = 16, Stroke = AccentBrush, StrokeThickness = 2.5,
        };
        Canvas.SetLeft(ring, 34);
        Canvas.SetTop(ring, 34);
        canvas.Children.Add(ring);

        canvas.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 };
        return canvas;
    }

    private static TextBlock MakeText(double size, FontWeight weight, Brush brush) => new()
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = size,
        FontWeight = weight,
        Foreground = brush,
        IsHitTestVisible = false,
        Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 },
    };
}
