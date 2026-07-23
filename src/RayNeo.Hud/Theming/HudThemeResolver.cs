// -----------------------------------------------------------------------------
// HudThemeResolver.cs
// Author: Kurt Mitchell
//
// Turns a theme reference (a bare name, a folder, or a theme.json path) into a
// concrete manifest path plus its containing folder. Bare names are searched
// under a "Themes" (or "themes") folder next to the executable and in the
// working directory, so `--theme aviator` finds Themes/aviator/theme.json that
// the build copies beside the app. Filesystem probes are injectable for tests.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>A resolved theme: the manifest file and the folder that holds its assets.</summary>
/// <param name="ManifestPath">Absolute (or relative) path to the theme.json file.</param>
/// <param name="Folder">The folder containing the manifest and its assets.</param>
public readonly record struct HudThemeLocation(string ManifestPath, string Folder);

/// <summary>Resolves theme references to concrete manifest locations.</summary>
public sealed class HudThemeResolver
{
    private const string ManifestFileName = "theme.json";

    private readonly IReadOnlyList<string> _searchRoots;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _directoryExists;

    /// <summary>Creates a resolver over the real filesystem and the default search roots.</summary>
    public HudThemeResolver()
        : this(DefaultSearchRoots(), File.Exists, Directory.Exists)
    {
    }

    /// <summary>Creates a resolver with explicit search roots and filesystem probes (for testing).</summary>
    public HudThemeResolver(
        IReadOnlyList<string> searchRoots, Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        _searchRoots = searchRoots ?? throw new ArgumentNullException(nameof(searchRoots));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _directoryExists = directoryExists ?? throw new ArgumentNullException(nameof(directoryExists));
    }

    /// <summary>The default roots: the app base directory and the current directory.</summary>
    public static IReadOnlyList<string> DefaultSearchRoots() =>
        new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };

    /// <summary>
    /// Resolves <paramref name="reference"/> to a manifest location.
    /// A reference ending in ".json" is used directly; an existing directory is
    /// treated as a theme folder; anything else is a name searched under the
    /// roots' <c>Themes</c>/<c>themes</c> folders.
    /// </summary>
    /// <exception cref="HudThemeException">Thrown when nothing matches.</exception>
    public HudThemeLocation Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new HudThemeException("No theme reference was provided.");
        }

        string trimmed = reference.Trim();

        // 1) An explicit theme.json path.
        if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            if (_fileExists(trimmed))
            {
                return new HudThemeLocation(trimmed, DirectoryOf(trimmed));
            }
            throw new HudThemeException($"Theme manifest not found: '{trimmed}'.");
        }

        // 2) An explicit theme folder containing theme.json.
        if (_directoryExists(trimmed))
        {
            string manifest = Path.Combine(trimmed, ManifestFileName);
            if (_fileExists(manifest))
            {
                return new HudThemeLocation(manifest, trimmed);
            }
            throw new HudThemeException($"Folder '{trimmed}' does not contain a {ManifestFileName}.");
        }

        // 3) A bare name searched under each root's Themes/themes folder.
        var probed = new List<string>();
        foreach (string root in _searchRoots)
        {
            foreach (string themesDir in new[] { "Themes", "themes" })
            {
                string folder = Path.Combine(root, themesDir, trimmed);
                string manifest = Path.Combine(folder, ManifestFileName);
                probed.Add(manifest);
                if (_fileExists(manifest))
                {
                    return new HudThemeLocation(manifest, folder);
                }
            }
        }

        throw new HudThemeException(
            $"Theme '{trimmed}' not found. Looked for: {string.Join("; ", probed)}.");
    }

    private static string DirectoryOf(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        return string.IsNullOrEmpty(dir) ? "." : dir;
    }
}
