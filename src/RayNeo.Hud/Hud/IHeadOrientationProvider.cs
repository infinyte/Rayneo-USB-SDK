// -----------------------------------------------------------------------------
// IHeadOrientationProvider.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Hud;

/// <summary>
/// Source of head orientation for the HUD. Implementations publish a snapshot
/// that the render loop reads via <see cref="Current"/>. The two shipped
/// implementations are hardware-backed and simulated, so the app runs with or
/// without the glasses.
/// </summary>
public interface IHeadOrientationProvider : IDisposable
{
    /// <summary>The most recent orientation snapshot. Cheap and thread-safe to read.</summary>
    HeadOrientation Current { get; }

    /// <summary>Short human-readable status for the HUD's connection chrome.</summary>
    string StatusText { get; }

    /// <summary>Begins producing samples.</summary>
    void Start();
}
