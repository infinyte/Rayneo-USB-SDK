// -----------------------------------------------------------------------------
// IPushToTalkSource.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// System-wide push-to-talk input. The HUD window is click-through and never
/// focused, so implementations must capture the key globally (a low-level
/// keyboard hook on Windows — see CLAUDE.md Phase 3); this interface keeps the
/// controller and its tests free of that interop. <see cref="Pressed"/> fires
/// once when the key goes down (no auto-repeat) and <see cref="Released"/>
/// once when it comes up.
/// </summary>
public interface IPushToTalkSource : IDisposable
{
    /// <summary>The push-to-talk key went down.</summary>
    event EventHandler? Pressed;

    /// <summary>The push-to-talk key came up.</summary>
    event EventHandler? Released;

    /// <summary>Begins monitoring for the key.</summary>
    void Start();
}
