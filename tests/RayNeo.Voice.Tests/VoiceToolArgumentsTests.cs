// -----------------------------------------------------------------------------
// VoiceToolArgumentsTests.cs
// Author: Kurt Mitchell
//
// JSON argument extraction for tools: required/optional accessors, type
// mismatches, and friendly error messages the model can act on.
// -----------------------------------------------------------------------------

using System.Text.Json;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class VoiceToolArgumentsTests
{
    private static JsonElement Parse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void GetRequiredString_ReadsValue()
    {
        var args = new VoiceToolArguments(Parse("{\"name\":\"tea\"}"));
        Assert.Equal("tea", args.GetRequiredString("name"));
    }

    [Fact]
    public void GetRequiredString_MissingProperty_ThrowsWithPropertyName()
    {
        var args = new VoiceToolArguments(Parse("{}"));
        var ex = Assert.Throws<VoiceToolArgumentException>(() => args.GetRequiredString("name"));
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public void GetRequiredString_WrongType_Throws()
    {
        var args = new VoiceToolArguments(Parse("{\"name\":42}"));
        Assert.Throws<VoiceToolArgumentException>(() => args.GetRequiredString("name"));
    }

    [Fact]
    public void GetRequiredNumber_ReadsIntegerAndFraction()
    {
        var args = new VoiceToolArguments(Parse("{\"a\":90,\"b\":2.5}"));
        Assert.Equal(90.0, args.GetRequiredNumber("a"));
        Assert.Equal(2.5, args.GetRequiredNumber("b"));
    }

    [Fact]
    public void GetRequiredNumber_MissingOrWrongType_Throws()
    {
        var args = new VoiceToolArguments(Parse("{\"a\":\"soon\"}"));
        Assert.Throws<VoiceToolArgumentException>(() => args.GetRequiredNumber("a"));
        Assert.Throws<VoiceToolArgumentException>(() => args.GetRequiredNumber("missing"));
    }

    [Fact]
    public void GetRequiredBoolean_ReadsValue()
    {
        var args = new VoiceToolArguments(Parse("{\"on\":true,\"off\":false}"));
        Assert.True(args.GetRequiredBoolean("on"));
        Assert.False(args.GetRequiredBoolean("off"));
    }

    [Fact]
    public void GetOptionalString_ReturnsDefaultWhenAbsentOrNull()
    {
        var args = new VoiceToolArguments(Parse("{\"x\":null}"));
        Assert.Equal("fallback", args.GetOptionalString("missing", "fallback"));
        Assert.Equal("fallback", args.GetOptionalString("x", "fallback"));
    }

    [Fact]
    public void GetOptionalNumber_ReturnsValueWhenPresent()
    {
        var args = new VoiceToolArguments(Parse("{\"n\":7}"));
        Assert.Equal(7.0, args.GetOptionalNumber("n", 1.0));
        Assert.Equal(1.0, args.GetOptionalNumber("missing", 1.0));
    }

    [Fact]
    public void NonObjectRoot_IsTreatedAsEmpty()
    {
        var args = new VoiceToolArguments(Parse("null"));
        Assert.Equal("d", args.GetOptionalString("k", "d"));
        Assert.Throws<VoiceToolArgumentException>(() => args.GetRequiredString("k"));
    }
}
