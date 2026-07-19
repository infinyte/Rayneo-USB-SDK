// -----------------------------------------------------------------------------
// IWhisperTranscriber.cs
// Author: Kurt Mitchell
//
// Seam for Whisper transcription: float samples in, transcript text out. Keeps
// the WhisperSpeechToText orchestrator free of the Whisper.net runtime and the
// on-disk model, so its Start/partial/Stop logic unit-tests with a fake. The
// model file is configuration; captured audio is never persisted (CLAUDE.md).
// -----------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>Transcribes in-memory 16 kHz mono samples into a single string.</summary>
public interface IWhisperTranscriber : IDisposable
{
    /// <summary>
    /// Transcribes the supplied 16 kHz mono samples and returns the joined
    /// transcript. Honors <paramref name="cancellationToken"/> so an abandoned
    /// partial pass can be cancelled.
    /// </summary>
    /// <param name="samples">16 kHz mono samples normalized to [-1, 1].</param>
    /// <param name="cancellationToken">Cancels an in-flight transcription.</param>
    Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken);
}
