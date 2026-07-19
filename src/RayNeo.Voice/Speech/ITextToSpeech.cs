// -----------------------------------------------------------------------------
// ITextToSpeech.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// Speaks assistant replies through the default audio device (the glasses when
/// routed there). <see cref="SpeakAsync"/> is fire-and-forget from the caller's
/// view; completion is reported via <see cref="SpeakCompleted"/>.
/// <see cref="Cancel"/> stops playback immediately for push-to-talk barge-in.
/// Engine-agnostic so a different synthesizer can replace the Windows one.
/// </summary>
public interface ITextToSpeech : IDisposable
{
    /// <summary>When true, <see cref="SpeakAsync"/> is a no-op (mute toggle).</summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Raised when speaking finishes — whether it completed naturally, was
    /// cancelled, or was suppressed because <see cref="IsMuted"/> was set. The
    /// loop relies on this single completion signal to leave the Speaking state.
    /// </summary>
    event EventHandler SpeakCompleted;

    /// <summary>Begins speaking <paramref name="text"/> on the default audio device.</summary>
    void SpeakAsync(string text);

    /// <summary>Immediately stops any in-progress speech (barge-in).</summary>
    void Cancel();
}
