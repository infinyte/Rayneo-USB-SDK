// -----------------------------------------------------------------------------
// MainWindow.xaml.cs
// Author: Kurt Mitchell
//
// The overlay window: borderless, transparent, always-on-top, click-through,
// placed full-screen on the target monitor. Builds the HUD scene (a bundled
// theme when one is selected, otherwise the built-in debug HUD), starts the
// compositor, and brings up the voice stack (degrading to HUD-only with an
// on-glass warning when voice cannot run).
// -----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Infinyte.RayNeo.Hud.Display;
using Infinyte.RayNeo.Hud.Interop;
using Infinyte.RayNeo.Hud.Theming;
using Infinyte.RayNeo.Hud.Voice;

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
    private readonly VoiceOptions _voiceOptions;
    private readonly string? _themeReference;
    private HudCompositor? _compositor;
    private VoiceRuntime? _voice;

    /// <summary>Creates the overlay for a specific display and orientation source.</summary>
    /// <param name="themeReference">
    /// Optional theme name/folder/manifest path; null uses the built-in HUD.
    /// </param>
    public MainWindow(
        DisplayInfo target, IHeadOrientationProvider provider, string? warning,
        VoiceOptions voiceOptions, string? themeReference = null)
    {
        InitializeComponent();
        _target = target;
        _provider = provider;
        _warning = warning;
        _voiceOptions = voiceOptions;
        _themeReference = themeReference;

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _compositor?.Stop();
            _voice?.Dispose();
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

        // Size the root canvas in DIPs from the *physical* monitor size and the
        // monitor's DPI scale (DIP = physical / scale). This is DPI-critical:
        //
        // The window is created on the primary monitor (this app is
        // PerMonitorV2-aware), then moved to the glasses' 150%-scaled display by
        // the SetWindowPos above. Windows reports the new scale via WM_DPICHANGED,
        // but that message is delivered and processed AFTER OnSourceInitialized
        // returns — so reading the DPI transform *here* still yields the primary's
        // 100% scale. A one-shot read at this point therefore over-sizes the
        // canvas by the scale ratio (e.g. a 1080-px-tall display becomes a
        // 1080-DIP canvas instead of 720), pushing every bottom- and
        // right-anchored element off the visible surface while fixed top-left
        // chrome still shows. Observed live on a 1920x1200@100% primary +
        // 1920x1080@150% glasses rig: the pitch/yaw/roll readout and warning
        // banner rendered below the bottom edge.
        //
        // Fix: recompute on every DPI and size change, using the scale that is
        // correct at that moment (the DpiChanged event carries it; VisualTreeHelper
        // reports the current one otherwise). The compositor reads the canvas size
        // each frame, so the scene self-corrects as soon as the real scale lands.
        DpiScale initial = VisualTreeHelper.GetDpi(this);
        SetCanvasSize(initial.DpiScaleX, initial.DpiScaleY, "init");
        DpiChanged += OnDpiChanged;
        SizeChanged += OnWindowSizeChanged;
    }

    private void OnDpiChanged(object? sender, DpiChangedEventArgs e) =>
        SetCanvasSize(e.NewDpi.DpiScaleX, e.NewDpi.DpiScaleY, "dpi-changed");

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        SetCanvasSize(dpi.DpiScaleX, dpi.DpiScaleY, "size-changed");
    }

    // Root canvas size in DIPs = physical monitor size / DPI scale.
    private void SetCanvasSize(double scaleX, double scaleY, string reason)
    {
        if (scaleX <= 0 || scaleY <= 0)
        {
            return;
        }
        Root.Width = _target.Width / scaleX;
        Root.Height = _target.Height / scaleY;
        LogDpi(reason, scaleX, scaleY);
    }

    // Diagnostic breadcrumb so canvas sizing can be verified without a console.
    // Appends to %TEMP%\rayneo-hud-dpi.log; never allowed to break rendering.
    private void LogDpi(string reason, double scaleX, double scaleY)
    {
        try
        {
            string line =
                $"[{DateTime.Now:HH:mm:ss.fff}] {reason}: scale=({scaleX:F3},{scaleY:F3}) " +
                $"target={_target.Width}x{_target.Height} " +
                $"root={Root.Width:F0}x{Root.Height:F0} " +
                $"window={ActualWidth:F0}x{ActualHeight:F0}" + Environment.NewLine;
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "rayneo-hud-dpi.log"), line);
        }
        catch
        {
            // Diagnostics only — never let logging break rendering.
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _compositor = new HudCompositor(Root, _provider);

        // Voice needs the compositor (pins render through it) and the UI thread
        // (the keyboard hook installs here). Null means voice is disabled; the
        // returned warning is surfaced on the glass alongside any display warning.
        _voice = VoiceRuntime.TryCreate(Dispatcher, _compositor, _provider, _voiceOptions, out string? voiceWarning);

        BuildScene(_compositor, voiceWarning);
        if (_voice is not null)
        {
            VoiceHudView.Attach(_compositor, _voice);
        }

        _provider.Start();
        _compositor.Start();
    }

    // ---- Scene --------------------------------------------------------------

    private void BuildScene(HudCompositor compositor, string? voiceWarning)
    {
        // Build a bundled theme when one is selected; on any theme failure fall
        // back to the built-in HUD and surface the reason on the glass.
        string? themeWarning = null;
        bool themed = false;
        if (_themeReference is not null)
        {
            try
            {
                HudTheme theme = new HudThemeLoader().Load(_themeReference);
                new HudThemeSceneBuilder(theme, compositor, _provider).Build();
                themed = true;
            }
            catch (HudThemeException ex)
            {
                themeWarning = $"Theme '{_themeReference}' not loaded ({ex.Message}) — using built-in HUD.";
            }
        }

        if (!themed)
        {
            BuildDefaultScene(compositor);
        }

        // Surface any startup warnings (no glasses / display fallback / voice off /
        // theme fallback) as bottom-left chrome, whichever scene is active.
        string? warning = Combine(Combine(_warning, voiceWarning), themeWarning);
        if (warning is not null)
        {
            TextBlock warn = MakeText(15, FontWeights.Normal, WarningBrush);
            warn.MaxWidth = 560;
            warn.TextWrapping = TextWrapping.Wrap;
            warn.Text = "⚠ " + warning;
            compositor.Add(new ScreenFixedElement(warn, ScreenAnchor.BottomLeft, margin: 24));
        }
    }

    // The built-in debug HUD: clock, connection status, live pitch/yaw/roll
    // readout, and a world-anchored crosshair.
    private void BuildDefaultScene(HudCompositor compositor)
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
    }

    private static string? Combine(string? first, string? second)
    {
        if (first is null)
        {
            return second;
        }
        return second is null ? first : $"{first}  |  {second}";
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
