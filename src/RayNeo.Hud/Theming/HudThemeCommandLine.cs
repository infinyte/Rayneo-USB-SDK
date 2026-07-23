// -----------------------------------------------------------------------------
// HudThemeCommandLine.cs
// Author: Kurt Mitchell
//
// Parses the theme selection from the command line and environment, mirroring
// the speech-engine parser: "--theme <name|folder|theme.json>" wins, otherwise
// the RAYNEO_HUD_THEME environment variable, otherwise none (built-in default).
// The environment lookup is injectable so the parser is testable without
// touching real process state.
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>The selected theme reference, or null for the built-in default HUD.</summary>
/// <param name="Reference">A theme name, folder path, or theme.json path; null means no theme.</param>
public sealed record ThemeSelection(string? Reference);

/// <summary>Parses HUD theme selection from arguments and the environment.</summary>
public static class HudThemeCommandLine
{
    /// <summary>Environment variable consulted when no <c>--theme</c> argument is present.</summary>
    public const string ThemeEnvironmentVariable = "RAYNEO_HUD_THEME";

    /// <summary>
    /// Parses <c>--theme &lt;reference&gt;</c>, falling back to
    /// <see cref="ThemeEnvironmentVariable"/>. Returns a selection whose
    /// <see cref="ThemeSelection.Reference"/> is null when neither is set.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="environment">
    /// Environment-variable lookup; defaults to the real process environment.
    /// </param>
    public static ThemeSelection ParseTheme(string[] args, Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        string? reference = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--theme")
            {
                reference = args[i + 1].Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(reference))
        {
            string? fromEnv = environment(ThemeEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                reference = fromEnv.Trim();
            }
        }

        return new ThemeSelection(string.IsNullOrEmpty(reference) ? null : reference);
    }
}
