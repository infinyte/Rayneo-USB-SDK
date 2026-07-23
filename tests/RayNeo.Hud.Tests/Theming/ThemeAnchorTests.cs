// -----------------------------------------------------------------------------
// ThemeAnchorTests.cs
// Author: Kurt Mitchell
//
// Anchor parsing: "world" maps to a world-locked anchor; the six screen anchors
// parse across casing and separator styles; unknown and empty inputs are rejected.
// -----------------------------------------------------------------------------

using Infinyte.RayNeo.Hud;
using Infinyte.RayNeo.Hud.Theming;

namespace RayNeo.Hud.Tests;

public sealed class ThemeAnchorTests
{
    [Fact]
    public void ParsesWorldAnchor()
    {
        Assert.True(ThemeAnchors.TryParse("world", out HudAnchorSpec anchor));
        Assert.Equal(HudAnchorKind.World, anchor.Kind);
    }

    [Theory]
    [InlineData("top-left", ScreenAnchor.TopLeft)]
    [InlineData("TopLeft", ScreenAnchor.TopLeft)]
    [InlineData("top_left", ScreenAnchor.TopLeft)]
    [InlineData("top-center", ScreenAnchor.TopCenter)]
    [InlineData("topcentre", ScreenAnchor.TopCenter)]
    [InlineData("top-right", ScreenAnchor.TopRight)]
    [InlineData("bottom left", ScreenAnchor.BottomLeft)]
    [InlineData("bottom-center", ScreenAnchor.BottomCenter)]
    [InlineData("BottomRight", ScreenAnchor.BottomRight)]
    public void ParsesScreenAnchors(string text, ScreenAnchor expected)
    {
        Assert.True(ThemeAnchors.TryParse(text, out HudAnchorSpec anchor));
        Assert.Equal(HudAnchorKind.Screen, anchor.Kind);
        Assert.Equal(expected, anchor.Screen);
    }

    [Theory]
    [InlineData("middle")]
    [InlineData("center")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsUnknownAnchors(string? text)
    {
        Assert.False(ThemeAnchors.TryParse(text, out _));
    }
}
