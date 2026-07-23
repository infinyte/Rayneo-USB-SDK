// -----------------------------------------------------------------------------
// HudThemeLoaderTests.cs
// Author: Kurt Mitchell
//
// Manifest parsing and validation via HudThemeLoader.Parse, which takes JSON
// text and an injectable asset-existence probe so it needs no real files or
// display. Covers a valid theme plus every validation failure: bad JSON,
// missing name, no elements, unknown type/anchor, missing text format,
// world-locked text, missing asset files, unsized panels, and negative slices.
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo.Hud.Theming;

namespace RayNeo.Hud.Tests;

public sealed class HudThemeLoaderTests
{
    private static readonly Func<string, bool> AllAssetsExist = _ => true;
    private static readonly Func<string, bool> NoAssetsExist = _ => false;

    private static HudTheme Parse(string json, Func<string, bool>? assetExists = null) =>
        new HudThemeLoader().Parse(json, "C:/themes/x", assetExists ?? AllAssetsExist);

    [Fact]
    public void ValidThemeParses()
    {
        HudTheme theme = Parse(
            @"{ ""name"": ""t"", ""elements"": [ { ""type"": ""text"", ""format"": ""hi"" } ] }");

        Assert.Equal("t", theme.Name);
        Assert.Single(theme.Elements);
        Assert.NotNull(theme.Defaults);
    }

    [Fact]
    public void ImageWithExistingAssetParses()
    {
        HudTheme theme = Parse(
            @"{ ""name"": ""t"", ""elements"": [ { ""type"": ""image"", ""anchor"": ""top-right"", ""asset"": ""logo.png"" } ] }",
            AllAssetsExist);

        Assert.Single(theme.Elements);
    }

    [Fact]
    public void InvalidJsonThrows()
    {
        Assert.Throws<HudThemeException>(() => Parse("{ this is not json"));
    }

    [Fact]
    public void MissingNameThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(@"{ ""elements"": [ { ""type"": ""text"", ""format"": ""hi"" } ] }"));
    }

    [Fact]
    public void NoElementsThrows()
    {
        Assert.Throws<HudThemeException>(() => Parse(@"{ ""name"": ""t"", ""elements"": [] }"));
    }

    [Fact]
    public void UnknownTypeThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(@"{ ""name"": ""t"", ""elements"": [ { ""type"": ""hologram"" } ] }"));
    }

    [Fact]
    public void UnknownAnchorThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(@"{ ""name"": ""t"", ""elements"": [ { ""type"": ""text"", ""format"": ""hi"", ""anchor"": ""middle"" } ] }"));
    }

    [Fact]
    public void TextMissingFormatThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(@"{ ""name"": ""t"", ""elements"": [ { ""type"": ""text"", ""anchor"": ""top-left"" } ] }"));
    }

    [Fact]
    public void WorldAnchoredTextIsRejected()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(@"{ ""name"": ""t"", ""elements"": [ { ""type"": ""text"", ""format"": ""hi"", ""anchor"": ""world"" } ] }"));
    }

    [Fact]
    public void ImageMissingAssetFileThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(
                @"{ ""name"": ""t"", ""elements"": [ { ""type"": ""image"", ""asset"": ""gone.png"" } ] }",
                NoAssetsExist));
    }

    [Fact]
    public void PanelWithoutSizeThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(
                @"{ ""name"": ""t"", ""elements"": [ { ""type"": ""panel"", ""asset"": ""panel.png"" } ] }",
                AllAssetsExist));
    }

    [Fact]
    public void NegativeSliceThrows()
    {
        Assert.Throws<HudThemeException>(() =>
            Parse(
                @"{ ""name"": ""t"", ""elements"": [ { ""type"": ""panel"", ""asset"": ""panel.png"", ""width"": 100, ""height"": 40, ""slice"": { ""left"": -4, ""top"": 4, ""right"": 4, ""bottom"": 4 } } ] }",
                AllAssetsExist));
    }
}
