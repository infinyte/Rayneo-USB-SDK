// -----------------------------------------------------------------------------
// HudThemeException.cs
// Author: Kurt Mitchell
//
// The single exception type raised for any theme problem — resolution, parsing,
// or validation. A single type lets the overlay catch one thing and surface its
// message on the glass, then fall back to the built-in default HUD.
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>Raised when a HUD theme cannot be found, parsed, or validated.</summary>
public sealed class HudThemeException : Exception
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    public HudThemeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an underlying cause.</summary>
    public HudThemeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
