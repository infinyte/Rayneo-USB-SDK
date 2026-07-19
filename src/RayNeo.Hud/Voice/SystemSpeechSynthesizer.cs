// -----------------------------------------------------------------------------
// SystemSpeechSynthesizer.cs
// Author: Kurt Mitchell
//
// ITextToSpeech over System.Speech's synthesizer, speaking on the default
// audio device (the glasses when Windows routes audio there). The interface
// contract: SpeakAsync always leads to exactly one SpeakCompleted — whether it
// finished, was cancelled (barge-in), or was suppressed by mute.
// -----------------------------------------------------------------------------

using System;
using System.Speech.Synthesis;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>Speaks assistant replies via <see cref="SpeechSynthesizer"/>.</summary>
public sealed class SystemSpeechSynthesizer : ITextToSpeech
{
    private readonly SpeechSynthesizer _synthesizer;
    private bool _disposed;

    /// <summary>Creates the synthesizer on the default audio device.</summary>
    public SystemSpeechSynthesizer()
    {
        _synthesizer = new SpeechSynthesizer();
        _synthesizer.SetOutputToDefaultAudioDevice();
        _synthesizer.SpeakCompleted += OnEngineSpeakCompleted;
    }

    /// <inheritdoc/>
    public bool IsMuted { get; set; }

    /// <inheritdoc/>
    public event EventHandler? SpeakCompleted;

    /// <inheritdoc/>
    public void SpeakAsync(string text)
    {
        if (_disposed)
        {
            return;
        }
        if (IsMuted || string.IsNullOrWhiteSpace(text))
        {
            // Suppressed: still signal completion so the loop leaves Speaking.
            SpeakCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }
        _synthesizer.SpeakAsyncCancelAll(); // one utterance at a time
        _synthesizer.SpeakAsync(text);
    }

    /// <inheritdoc/>
    public void Cancel()
    {
        if (!_disposed)
        {
            // The engine raises SpeakCompleted (cancelled) for the pruned prompt,
            // which OnEngineSpeakCompleted forwards — the single completion signal.
            _synthesizer.SpeakAsyncCancelAll();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _synthesizer.SpeakCompleted -= OnEngineSpeakCompleted;
        _synthesizer.SpeakAsyncCancelAll();
        _synthesizer.Dispose();
    }

    private void OnEngineSpeakCompleted(object? sender, SpeakCompletedEventArgs e) =>
        SpeakCompleted?.Invoke(this, EventArgs.Empty);
}
