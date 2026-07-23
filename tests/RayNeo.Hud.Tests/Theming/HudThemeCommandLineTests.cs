// -----------------------------------------------------------------------------
// HudThemeCommandLineTests.cs
// Author: Kurt Mitchell
//
// Theme-selection parsing: the built-in default when nothing is set, an
// explicit --theme argument, the RAYNEO_HUD_THEME environment fallback (and
// that an explicit argument overrides it), and whitespace trimming.
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo.Hud.Theming;

namespace RayNeo.Hud.Tests;

public sealed class HudThemeCommandLineTests
{
    private static Func<string, string?> NoEnv => _ => null;

    [Fact]
    public void DefaultsToNoTheme()
    {
        ThemeSelection selection = HudThemeCommandLine.ParseTheme(Array.Empty<string>(), NoEnv);
        Assert.Null(selection.Reference);
    }

    [Fact]
    public void ExplicitThemeNameIsUsed()
    {
        ThemeSelection selection = HudThemeCommandLine.ParseTheme(new[] { "--theme", "aviator" }, NoEnv);
        Assert.Equal("aviator", selection.Reference);
    }

    [Fact]
    public void FallsBackToEnvironmentVariable()
    {
        Func<string, string?> env = name =>
            name == HudThemeCommandLine.ThemeEnvironmentVariable ? "night" : null;

        ThemeSelection selection = HudThemeCommandLine.ParseTheme(Array.Empty<string>(), env);
        Assert.Equal("night", selection.Reference);
    }

    [Fact]
    public void ExplicitThemeOverridesEnvironmentVariable()
    {
        Func<string, string?> env = _ => "night";

        ThemeSelection selection = HudThemeCommandLine.ParseTheme(new[] { "--theme", "aviator" }, env);
        Assert.Equal("aviator", selection.Reference);
    }

    [Fact]
    public void ThemeFlagWithoutValueFallsBackToEnvironment()
    {
        Func<string, string?> env = _ => "night";

        // A trailing "--theme" with no following value is ignored; the env wins.
        ThemeSelection selection = HudThemeCommandLine.ParseTheme(new[] { "--theme" }, env);
        Assert.Equal("night", selection.Reference);
    }

    [Fact]
    public void ThemeReferenceIsTrimmed()
    {
        ThemeSelection selection = HudThemeCommandLine.ParseTheme(new[] { "--theme", "  aviator  " }, NoEnv);
        Assert.Equal("aviator", selection.Reference);
    }
}
