// -----------------------------------------------------------------------------
// WhisperNetTranscriberTests.cs
// Author: Kurt Mitchell
//
// The Whisper.net adapter's model-path guard: a missing or non-existent model
// throws FileNotFoundException with an actionable message. No model is loaded or
// downloaded in tests — only the pre-load validation is exercised.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using Infinyte.RayNeo.Hud.Voice;

namespace RayNeo.Hud.Tests;

public sealed class WhisperNetTranscriberTests
{
    [Fact]
    public void MissingFileThrowsFileNotFound()
    {
        string absent = Path.Combine(Path.GetTempPath(), "no-such-whisper-model-" + Guid.NewGuid().ToString("N") + ".bin");
        Assert.Throws<FileNotFoundException>(() => new WhisperNetTranscriber(absent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPathThrowsFileNotFound(string path)
    {
        Assert.Throws<FileNotFoundException>(() => new WhisperNetTranscriber(path));
    }

    [Fact]
    public void MessageIsActionable()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => new WhisperNetTranscriber(@"Z:\definitely\missing.bin"));

        // Points the user at how to supply a model.
        Assert.Contains("--whisper-model", ex.Message);
        Assert.Contains(VoiceCommandLine.ModelPathEnvironmentVariable, ex.Message);
    }
}
