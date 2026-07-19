// -----------------------------------------------------------------------------
// HudCompositor.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace Infinyte.RayNeo.Hud;

/// <summary>
/// Drives the HUD render loop. It runs on the UI thread off
/// <see cref="CompositionTarget.Rendering"/> (throttled to ~60 fps) and reads
/// the orientation provider's latest snapshot — completely decoupled from the
/// ~495 Hz sample thread that produces those snapshots.
/// </summary>
public sealed class HudCompositor
{
    private static readonly TimeSpan FramePeriod = TimeSpan.FromSeconds(1.0 / 60.0);

    private readonly Canvas _canvas;
    private readonly IHeadOrientationProvider _provider;
    private readonly List<HudElement> _elements = new();
    private readonly double _horizontalFovDeg;
    private readonly double _verticalFovDeg;
    private TimeSpan _lastFrame;
    private bool _haveLastFrame;

    /// <summary>Creates a compositor rendering onto <paramref name="canvas"/>.</summary>
    /// <param name="horizontalFovDeg">Display horizontal FOV; RayNeo Air 4 Pro ≈ 40°.</param>
    /// <param name="verticalFovDeg">Display vertical FOV; RayNeo Air 4 Pro ≈ 23°.</param>
    public HudCompositor(
        Canvas canvas, IHeadOrientationProvider provider,
        double horizontalFovDeg = 40.0, double verticalFovDeg = 23.0)
    {
        _canvas = canvas;
        _provider = provider;
        _horizontalFovDeg = horizontalFovDeg;
        _verticalFovDeg = verticalFovDeg;
    }

    /// <summary>Registers an element and adds its visual to the canvas.</summary>
    public void Add(HudElement element)
    {
        _elements.Add(element);
        _canvas.Children.Add(element.Visual);
    }

    /// <summary>Unregisters an element and removes its visual from the canvas.</summary>
    public void Remove(HudElement element)
    {
        _elements.Remove(element);
        _canvas.Children.Remove(element.Visual);
    }

    /// <summary>Starts the render loop.</summary>
    public void Start() => CompositionTarget.Rendering += OnRendering;

    /// <summary>Stops the render loop.</summary>
    public void Stop() => CompositionTarget.Rendering -= OnRendering;

    private void OnRendering(object? sender, EventArgs e)
    {
        // CompositionTarget.Rendering fires at the display refresh rate (which
        // can exceed 60 Hz); throttle to a steady 60 fps.
        if (e is RenderingEventArgs rendering)
        {
            if (_haveLastFrame && rendering.RenderingTime - _lastFrame < FramePeriod)
            {
                return;
            }
            _lastFrame = rendering.RenderingTime;
            _haveLastFrame = true;
        }

        var viewport = new HudViewport(
            _canvas.ActualWidth, _canvas.ActualHeight, _horizontalFovDeg, _verticalFovDeg);
        if (!viewport.IsValid)
        {
            return; // canvas not laid out yet
        }

        HeadOrientation orientation = _provider.Current;
        foreach (HudElement element in _elements)
        {
            element.Arrange(orientation, in viewport);
        }
    }
}
