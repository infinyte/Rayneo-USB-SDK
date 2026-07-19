// -----------------------------------------------------------------------------
// VoiceCommandLineTests.cs
// Author: Kurt Mitchell
//
// Speech-engine argument parsing: the System default, --stt whisper with an
// explicit model, the RAYNEO_WHISPER_MODEL environment fallback (and that an
// explicit path overrides it), and rejection of an unknown --stt value.
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo.Hud.Voice;

namespace RayNeo.Hud.Tests;

public sealed class VoiceCommandLineTests
{
    private static Func<string, string?> NoEnv => _ => null;

    [Fact]
    public void DefaultsToSystemEngine()
    {
        SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(Array.Empty<string>(), NoEnv);
        Assert.Equal(SpeechEngineKind.System, selection.Engine);
        Assert.Null(selection.WhisperModelPath);
    }

    [Fact]
    public void ExplicitSystemEngineIsAccepted()
    {
        SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(new[] { "--stt", "system" }, NoEnv);
        Assert.Equal(SpeechEngineKind.System, selection.Engine);
    }

    [Fact]
    public void WhisperEngineWithModelPath()
    {
        SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(
            new[] { "--stt", "whisper", "--whisper-model", @"C:\models\ggml-base.en.bin" }, NoEnv);

        Assert.Equal(SpeechEngineKind.Whisper, selection.Engine);
        Assert.Equal(@"C:\models\ggml-base.en.bin", selection.WhisperModelPath);
    }

    [Fact]
    public void EngineIsCaseInsensitive()
    {
        SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(new[] { "--stt", "Whisper" }, NoEnv);
        Assert.Equal(SpeechEngineKind.Whisper, selection.Engine);
    }

    [Fact]
    public void ModelPathFallsBackToEnvironmentVariable()
    {
        Func<string, string?> env = name =>
            name == VoiceCommandLine.ModelPathEnvironmentVariable ? @"D:\ggml-small.bin" : null;

        SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(new[] { "--stt", "whisper" }, env);

        Assert.Equal(SpeechEngineKind.Whisper, selection.Engine);
        Assert.Equal(@"D:\ggml-small.bin", selection.WhisperModelPath);
    }

    [Fact]
    public void ExplicitModelPathOverridesEnvironmentVariable()
    {
        Func<string, string?> env = _ => @"D:\from-env.bin";

        SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(
            new[] { "--whisper-model", @"C:\from-cli.bin" }, env);

        Assert.Equal(@"C:\from-cli.bin", selection.WhisperModelPath);
    }

    [Fact]
    public void UnknownEngineValueThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            VoiceCommandLine.ParseSpeechEngine(new[] { "--stt", "vosk" }, NoEnv));
    }
}
