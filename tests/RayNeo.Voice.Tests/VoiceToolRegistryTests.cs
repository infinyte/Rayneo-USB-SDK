// -----------------------------------------------------------------------------
// VoiceToolRegistryTests.cs
// Author: Kurt Mitchell
//
// Registry behaviour: registration, duplicate/invalid-name rejection, lookup,
// and definition ordering. Pure — no audio, network, or UI (CLAUDE.md Phase 3).
// -----------------------------------------------------------------------------

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class VoiceToolRegistryTests
{
    private static DelegateVoiceTool MakeTool(string name) => new(
        name,
        $"Test tool {name}.",
        Array.Empty<VoiceToolParameter>(),
        (_, _) => Task.FromResult("ok"));

    [Fact]
    public void NewRegistry_IsEmpty()
    {
        var registry = new VoiceToolRegistry();
        Assert.Empty(registry.Tools);
    }

    [Fact]
    public void Register_MakesToolAvailable_InRegistrationOrder()
    {
        var registry = new VoiceToolRegistry();
        registry.Register(MakeTool("alpha"));
        registry.Register(MakeTool("beta"));

        Assert.Equal(2, registry.Tools.Count);
        Assert.Equal("alpha", registry.Tools[0].Name);
        Assert.Equal("beta", registry.Tools[1].Name);
    }

    [Fact]
    public void TryGet_FindsRegisteredTool()
    {
        var registry = new VoiceToolRegistry();
        registry.Register(MakeTool("start_timer"));

        Assert.True(registry.TryGet("start_timer", out IVoiceTool? tool));
        Assert.Equal("start_timer", tool!.Name);
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownTool()
    {
        var registry = new VoiceToolRegistry();
        Assert.False(registry.TryGet("nope", out IVoiceTool? tool));
        Assert.Null(tool);
    }

    [Fact]
    public void Register_RejectsDuplicateName()
    {
        var registry = new VoiceToolRegistry();
        registry.Register(MakeTool("dup"));
        Assert.Throws<InvalidOperationException>(() => registry.Register(MakeTool("dup")));
        Assert.Single(registry.Tools);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("bad!chars")]
    [InlineData("way_too_long_name_way_too_long_name_way_too_long_name_way_too_long_name")]
    public void Register_RejectsInvalidToolName(string name)
    {
        var registry = new VoiceToolRegistry();
        Assert.Throws<ArgumentException>(() => registry.Register(MakeTool(name)));
    }

    [Fact]
    public void Register_RejectsNullTool()
    {
        var registry = new VoiceToolRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public async Task DelegateTool_InvokesCallbackWithArguments()
    {
        string? seen = null;
        var tool = new DelegateVoiceTool(
            "echo",
            "Echoes back.",
            new[] { new VoiceToolParameter("text", "Text to echo.", VoiceToolParameterType.String, IsRequired: true) },
            (args, _) =>
            {
                seen = args.GetRequiredString("text");
                return Task.FromResult($"echo: {seen}");
            });

        using JsonDocument doc = JsonDocument.Parse("{\"text\":\"hi\"}");
        string result = await tool.ExecuteAsync(doc.RootElement.Clone(), CancellationToken.None);

        Assert.Equal("hi", seen);
        Assert.Equal("echo: hi", result);
    }
}
