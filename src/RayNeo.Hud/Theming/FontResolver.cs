// -----------------------------------------------------------------------------
// FontResolver.cs
// Author: Kurt Mitchell
//
// Resolves a theme font specification into a WPF FontFamily. A spec is either a
// system family name ("Consolas", "Segoe UI") or a bundled font file
// (".ttf"/".otf") dropped in the theme folder. Anything that fails to load
// falls back to the default monospace family so a bad font never blanks the HUD.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Windows.Media;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>Resolves theme font specs to <see cref="FontFamily"/> instances.</summary>
public static class FontResolver
{
    /// <summary>The fallback family used when a spec is empty or fails to load.</summary>
    public static FontFamily Default { get; } = new("Consolas");

    /// <summary>
    /// Resolves <paramref name="fontSpec"/> — a system family name or a bundled
    /// <c>.ttf</c>/<c>.otf</c> file relative to <paramref name="themeFolder"/> —
    /// to a <see cref="FontFamily"/>, falling back to <see cref="Default"/>.
    /// </summary>
    public static FontFamily Resolve(string? fontSpec, string themeFolder)
    {
        if (string.IsNullOrWhiteSpace(fontSpec))
        {
            return Default;
        }

        string spec = fontSpec.Trim();

        bool isFile =
            spec.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
            spec.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);

        if (isFile)
        {
            try
            {
                string path = Path.IsPathRooted(spec) ? spec : Path.Combine(themeFolder, spec);
                if (File.Exists(path))
                {
                    foreach (FontFamily family in Fonts.GetFontFamilies(path))
                    {
                        return family; // the first family declared in the file
                    }
                }
            }
            catch
            {
                // Fall through to the default on any font-loading failure.
            }
            return Default;
        }

        try
        {
            return new FontFamily(spec);
        }
        catch
        {
            return Default;
        }
    }
}
