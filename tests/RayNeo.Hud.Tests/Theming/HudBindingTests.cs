// -----------------------------------------------------------------------------
// HudBindingTests.cs
// Author: Kurt Mitchell
//
// The theme text binding vocabulary: numeric tokens with format specs, multiple
// tokens in one string, the clock/status/connection tokens, literal braces,
// unknown tokens left verbatim, and empty templates.
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo.Hud.Theming;

namespace RayNeo.Hud.Tests;

public sealed class HudBindingTests
{
    private static HudBindingValues Values(
        float pitch = 0f, float yaw = 0f, float roll = 0f, float temp = 0f,
        string status = "", string connection = "", DateTime now = default) =>
        new(pitch, yaw, roll, temp, status, connection, now);

    [Fact]
    public void FormatsNumericTokenWithSpec()
    {
        string result = HudBinding.Format("pitch {pitch:F1}", Values(pitch: 12.34f));
        Assert.Equal("pitch 12.3", result);
    }

    [Fact]
    public void ResolvesMultipleTokens()
    {
        string result = HudBinding.Format(
            "p {pitch:F1} y {yaw:F1} r {roll:F1}", Values(pitch: 1f, yaw: 2f, roll: 3f));
        Assert.Equal("p 1.0 y 2.0 r 3.0", result);
    }

    [Fact]
    public void TokenWithoutFormatUsesInvariantToString()
    {
        string result = HudBinding.Format("{yaw}", Values(yaw: 1.5f));
        Assert.Equal("1.5", result);
    }

    [Fact]
    public void ClockTokenAppliesDateFormat()
    {
        var now = new DateTime(2026, 7, 23, 13, 5, 9);
        string result = HudBinding.Format("{clock:HH:mm:ss}", Values(now: now));
        Assert.Equal("13:05:09", result);
    }

    [Fact]
    public void StatusAndConnectionTokensResolve()
    {
        string result = HudBinding.Format(
            "{status} / {connection}", Values(status: "glasses connected", connection: "simulated"));
        Assert.Equal("glasses connected / simulated", result);
    }

    [Fact]
    public void TemperatureTokenResolves()
    {
        string result = HudBinding.Format("{temp:F1}°C", Values(temp: 29.9f));
        Assert.Equal("29.9°C", result);
    }

    [Fact]
    public void UnknownTokenIsLeftLiteral()
    {
        string result = HudBinding.Format("{bogus} {pitch:F0}", Values(pitch: 5f));
        Assert.Equal("{bogus} 5", result);
    }

    [Fact]
    public void EscapedBracesAreLiteral()
    {
        string result = HudBinding.Format("{{pitch}} = {pitch:F0}", Values(pitch: 7f));
        Assert.Equal("{pitch} = 7", result);
    }

    [Fact]
    public void NullTemplateReturnsEmpty()
    {
        Assert.Equal(string.Empty, HudBinding.Format(null, Values()));
    }

    [Fact]
    public void EmptyTemplateReturnsEmpty()
    {
        Assert.Equal(string.Empty, HudBinding.Format(string.Empty, Values()));
    }
}
