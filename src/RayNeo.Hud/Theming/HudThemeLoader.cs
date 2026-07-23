// -----------------------------------------------------------------------------
// HudThemeLoader.cs
// Author: Kurt Mitchell
//
// Reads and validates a theme manifest into a HudTheme ready for the scene
// builder. All the fallible, rule-checking logic lives here (and in Parse,
// which takes JSON text and an injectable asset-existence probe) so validation
// is unit-testable without a real file or a display. Every failure is a
// HudThemeException carrying a precise, author-facing message.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>A validated, ready-to-render HUD theme.</summary>
public sealed class HudTheme
{
    /// <summary>Creates a validated theme.</summary>
    public HudTheme(string name, string folder, HudThemeManifest manifest)
    {
        Name = name;
        Folder = folder;
        Manifest = manifest;
    }

    /// <summary>Theme name.</summary>
    public string Name { get; }

    /// <summary>Folder containing the manifest and its assets.</summary>
    public string Folder { get; }

    /// <summary>The parsed manifest.</summary>
    public HudThemeManifest Manifest { get; }

    /// <summary>Theme-wide defaults (never null once validated; a synthesized empty set if omitted).</summary>
    public HudThemeDefaults Defaults => Manifest.Defaults ??= new HudThemeDefaults();

    /// <summary>The validated element list.</summary>
    public IReadOnlyList<HudThemeElement> Elements => Manifest.Elements ?? new List<HudThemeElement>();
}

/// <summary>Loads and validates HUD themes.</summary>
public sealed class HudThemeLoader
{
    /// <summary>The element type keywords a manifest may use.</summary>
    public static IReadOnlyList<string> KnownTypes { get; } =
        new[] { "text", "image", "panel", "crosshair" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Resolves and loads a theme by reference (name, folder, or theme.json path).</summary>
    public HudTheme Load(string reference) => Load(new HudThemeResolver().Resolve(reference));

    /// <summary>Loads a theme from an already-resolved location.</summary>
    public HudTheme Load(HudThemeLocation location)
    {
        string json;
        try
        {
            json = File.ReadAllText(location.ManifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HudThemeException(
                $"Could not read theme manifest '{location.ManifestPath}': {ex.Message}", ex);
        }

        return Parse(json, location.Folder, File.Exists);
    }

    /// <summary>
    /// Parses and validates manifest <paramref name="json"/> for a theme rooted
    /// at <paramref name="folder"/>, using <paramref name="assetExists"/> to
    /// verify referenced asset files. Pure: no real IO beyond the injected probe.
    /// </summary>
    /// <exception cref="HudThemeException">Thrown on any parse or validation failure.</exception>
    public HudTheme Parse(string json, string folder, Func<string, bool> assetExists)
    {
        HudThemeManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<HudThemeManifest>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new HudThemeException($"Theme manifest is not valid JSON: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new HudThemeException("Theme manifest is empty.");
        }
        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new HudThemeException("Theme manifest is missing a \"name\".");
        }
        if (manifest.Elements is null || manifest.Elements.Count == 0)
        {
            throw new HudThemeException($"Theme '{manifest.Name}' declares no elements.");
        }

        for (int i = 0; i < manifest.Elements.Count; i++)
        {
            Validate(manifest.Name!, i, manifest.Elements[i], folder, assetExists);
        }

        return new HudTheme(manifest.Name!, folder, manifest);
    }

    private static void Validate(
        string themeName, int index, HudThemeElement element, string folder, Func<string, bool> assetExists)
    {
        string where = $"theme '{themeName}' element #{index + 1}";

        if (string.IsNullOrWhiteSpace(element.Type))
        {
            throw new HudThemeException($"{where} is missing a \"type\".");
        }

        string type = element.Type!.Trim().ToLowerInvariant();
        if (Array.IndexOf(new[] { "text", "image", "panel", "crosshair" }, type) < 0)
        {
            throw new HudThemeException(
                $"{where} has unknown type '{element.Type}'. Use one of: {string.Join(", ", KnownTypes)}.");
        }

        // Anchor, if present, must parse.
        HudAnchorSpec anchor = default;
        bool hasAnchor = element.Anchor is not null;
        if (hasAnchor && !ThemeAnchors.TryParse(element.Anchor, out anchor))
        {
            throw new HudThemeException(
                $"{where} has unknown anchor '{element.Anchor}'. Use world or a screen corner/edge such as top-left.");
        }

        bool isWorld = hasAnchor && anchor.Kind == HudAnchorKind.World;

        switch (type)
        {
            case "text":
                if (string.IsNullOrEmpty(element.Format))
                {
                    throw new HudThemeException($"{where} (text) is missing a \"format\" string.");
                }
                if (isWorld)
                {
                    throw new HudThemeException(
                        $"{where} (text) must use a screen anchor; world-locked live text is not supported.");
                }
                break;

            case "image":
                RequireAsset(where, element, folder, assetExists);
                if (isWorld)
                {
                    RequirePositiveSize(where, element);
                }
                break;

            case "panel":
                RequireAsset(where, element, folder, assetExists);
                RequirePositiveSize(where, element);
                ValidateSlice(where, element.Slice);
                break;

            case "crosshair":
                // Asset is optional (a built-in vector crosshair is used when absent).
                if (!string.IsNullOrEmpty(element.Asset))
                {
                    RequireAsset(where, element, folder, assetExists);
                }
                break;
        }
    }

    private static void RequireAsset(
        string where, HudThemeElement element, string folder, Func<string, bool> assetExists)
    {
        if (string.IsNullOrWhiteSpace(element.Asset))
        {
            throw new HudThemeException($"{where} is missing an \"asset\" file name.");
        }

        string path = Path.Combine(folder, element.Asset!);
        if (!assetExists(path))
        {
            throw new HudThemeException($"{where} references a missing asset file '{element.Asset}'.");
        }
    }

    private static void RequirePositiveSize(string where, HudThemeElement element)
    {
        if (element.Width is not > 0 || element.Height is not > 0)
        {
            throw new HudThemeException($"{where} requires positive \"width\" and \"height\".");
        }
    }

    private static void ValidateSlice(string where, HudThemeSlice? slice)
    {
        if (slice is null)
        {
            return;
        }
        if (slice.Left < 0 || slice.Top < 0 || slice.Right < 0 || slice.Bottom < 0)
        {
            throw new HudThemeException($"{where} has negative nine-slice margins.");
        }
    }
}
