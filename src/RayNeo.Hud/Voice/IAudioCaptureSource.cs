// -----------------------------------------------------------------------------
// IAudioCaptureSource.cs
// Author: Kurt Mitchell
//
// Seam for push-to-talk microphone capture. Isolates the NAudio device plumbing
// from the WhisperSpeechToText orchestrator so the orchestration logic unit-tests
// with no microphone. Capture runs strictly between Start and Stop and samples
// live only in memory — nothing is ever written to disk (CLAUDE.md Phase 3).
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>
/// A push-to-talk microphone source that delivers 16 kHz mono audio as blocks of
/// floating-point samples. <see cref="Start"/> opens the device and begins
/// raising <see cref="SamplesAvailable"/>; <see cref="Stop"/> ends capture.
/// </summary>
public interface IAudioCaptureSource : IDisposable
{
    /// <summary>
    /// Raised for each captured block of 16 kHz mono samples, normalized to the
    /// range [-1, 1]. Fires on a capture/thread-pool thread while capturing.
    /// </summary>
    event EventHandler<float[]>? SamplesAvailable;

    /// <summary>Raised when capture fails (device unplugged, driver error).</summary>
    event EventHandler<Exception>? Failed;

    /// <summary>Opens the microphone and begins capturing.</summary>
    void Start();

    /// <summary>Stops capturing and releases the microphone.</summary>
    void Stop();
}
