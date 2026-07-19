// -----------------------------------------------------------------------------
// WhisperNetTranscriber.cs
// Author: Kurt Mitchell
//
// IWhisperTranscriber over Whisper.net (managed whisper.cpp bindings). Loads a
// ggml model once at construction and runs a CPU transcription per call over the
// in-memory 16 kHz mono samples handed in by the orchestrator. The model file is
// configuration; the audio it transcribes is never written to disk.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>Local Whisper transcription backed by a ggml model file.</summary>
public sealed class WhisperNetTranscriber : IWhisperTranscriber
{
    private readonly WhisperFactory _factory;

    /// <summary>Loads the ggml model at <paramref name="modelPath"/>.</summary>
    /// <param name="modelPath">Path to a whisper.cpp ggml model (e.g. <c>ggml-base.en.bin</c>).</param>
    /// <exception cref="FileNotFoundException">
    /// The model path is missing or does not point at an existing file.
    /// </exception>
    public WhisperNetTranscriber(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Whisper model not found at '{modelPath}'. Supply --whisper-model <path> or set " +
                "the RAYNEO_WHISPER_MODEL environment variable to a ggml model such as " +
                "ggml-base.en.bin, downloadable from " +
                "https://huggingface.co/ggerganov/whisper.cpp/tree/main.",
                modelPath ?? "(null)");
        }

        _factory = WhisperFactory.FromPath(modelPath);
    }

    /// <inheritdoc/>
    public async Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(samples);

        // A processor per call keeps state from bleeding across turns; the loaded
        // model in _factory is reused, so only the lightweight processor is rebuilt.
        await using var processor = _factory.CreateBuilder().WithLanguage("en").Build();

        var transcript = new StringBuilder();
        await foreach (SegmentData segment in processor.ProcessAsync(samples, cancellationToken)
            .ConfigureAwait(false))
        {
            transcript.Append(segment.Text);
        }
        return transcript.ToString().Trim();
    }

    /// <inheritdoc/>
    public void Dispose() => _factory.Dispose();
}
