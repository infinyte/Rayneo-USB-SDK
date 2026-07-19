// -----------------------------------------------------------------------------
// SimulatedOrientationProvider.cs
// Author: Kurt Mitchell
//
// Gentle synthetic sweep so the HUD — world-anchoring in particular — is
// demonstrable without hardware. Per CLAUDE.md, everything but the live demo
// must run without the glasses, so the app falls back to this when the device
// cannot be opened.
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace Infinyte.RayNeo.Hud;

/// <summary>Produces a slow, smooth orientation sweep with no hardware.</summary>
public sealed class SimulatedOrientationProvider : IHeadOrientationProvider
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <inheritdoc/>
    public HeadOrientation Current
    {
        get
        {
            double t = _clock.Elapsed.TotalSeconds;
            // Amplitudes chosen so the crosshair sweeps out to the FOV edges,
            // exercising the clamp-and-fade path.
            float yaw = (float)(25.0 * Math.Sin(t * 0.50));
            float pitch = (float)(12.0 * Math.Sin(t * 0.35));
            float roll = (float)(8.0 * Math.Sin(t * 0.70));
            return new HeadOrientation(pitch, roll, yaw, isLive: false, isSimulated: true, 0f);
        }
    }

    /// <inheritdoc/>
    public string StatusText => "SIMULATED — no glasses";

    /// <inheritdoc/>
    public void Start() { }

    /// <inheritdoc/>
    public void Dispose() { }
}
