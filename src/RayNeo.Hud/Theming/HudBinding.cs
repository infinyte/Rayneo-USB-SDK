// -----------------------------------------------------------------------------
// HudBinding.cs
// Author: Kurt Mitchell
//
// The theme text binding vocabulary. A theme author writes a format string with
// named tokens — "pitch {pitch:F1}°   yaw {yaw:F1}°" — and this resolves those
// tokens against a per-frame snapshot of live HUD values. Pure and fully
// testable: the same inputs always produce the same string.
//
// Supported tokens (case-insensitive): pitch, yaw, roll, temp/temperature,
// status, connection, clock/time, date. An optional ":format" applies standard
// .NET formatting (e.g. {clock:HH:mm:ss}, {pitch:F1}). "{{" and "}}" emit
// literal braces; an unknown token is left verbatim so typos are visible, not
// fatal.
// -----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>Immutable per-frame values a theme's text tokens can reference.</summary>
public readonly struct HudBindingValues
{
    /// <summary>Creates a value snapshot.</summary>
    public HudBindingValues(
        float pitch, float yaw, float roll, float temperature,
        string statusText, string connection, DateTime now)
    {
        Pitch = pitch;
        Yaw = yaw;
        Roll = roll;
        Temperature = temperature;
        StatusText = statusText;
        Connection = connection;
        Now = now;
    }

    /// <summary>Head pitch in degrees.</summary>
    public float Pitch { get; }

    /// <summary>Head yaw in degrees.</summary>
    public float Yaw { get; }

    /// <summary>Head roll in degrees.</summary>
    public float Roll { get; }

    /// <summary>Die temperature in °C.</summary>
    public float Temperature { get; }

    /// <summary>Provider connection status text.</summary>
    public string StatusText { get; }

    /// <summary>Connection word: "connected", "simulated", or "disconnected".</summary>
    public string Connection { get; }

    /// <summary>Wall-clock time used by clock/time/date tokens.</summary>
    public DateTime Now { get; }
}

/// <summary>Resolves theme text tokens against a <see cref="HudBindingValues"/> snapshot.</summary>
public static class HudBinding
{
    // Matches an escaped brace ("{{" or "}}") or a token "{name}" / "{name:fmt}".
    private static readonly Regex TokenPattern = new(
        @"\{\{|\}\}|\{(?<name>[A-Za-z][A-Za-z0-9_]*)(?::(?<fmt>[^{}]*))?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Expands <paramref name="template"/>, replacing tokens with values from
    /// <paramref name="values"/>. Returns an empty string for a null/empty template.
    /// </summary>
    public static string Format(string? template, in HudBindingValues values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        // Copy to a local so the MatchEvaluator can capture it (an 'in' parameter
        // cannot be captured by a lambda).
        HudBindingValues snapshot = values;

        return TokenPattern.Replace(template, match =>
        {
            if (match.Value == "{{")
            {
                return "{";
            }
            if (match.Value == "}}")
            {
                return "}";
            }

            string name = match.Groups["name"].Value;
            if (!TryResolve(name, snapshot, out object? value))
            {
                return match.Value; // leave an unknown token literal
            }
            if (value is null)
            {
                return string.Empty;
            }

            string? format = match.Groups["fmt"].Success && match.Groups["fmt"].Value.Length > 0
                ? match.Groups["fmt"].Value
                : null;

            if (format is not null && value is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }
            return value.ToString() ?? string.Empty;
        });
    }

    /// <summary>Resolves a single token name to its current value.</summary>
    /// <returns>True if the token is known; false to leave it literal.</returns>
    public static bool TryResolve(string name, in HudBindingValues values, out object? value)
    {
        switch (name.ToLowerInvariant())
        {
            case "pitch":
                value = values.Pitch;
                return true;
            case "yaw":
                value = values.Yaw;
                return true;
            case "roll":
                value = values.Roll;
                return true;
            case "temp":
            case "temperature":
                value = values.Temperature;
                return true;
            case "status":
                value = values.StatusText;
                return true;
            case "connection":
                value = values.Connection;
                return true;
            case "clock":
            case "time":
            case "date":
                value = values.Now;
                return true;
            default:
                value = null;
                return false;
        }
    }
}
