// -----------------------------------------------------------------------------
// SystemSpeechToText.cs
// Author: Kurt Mitchell
//
// ISpeechToText over System.Speech's offline dictation engine. Capture runs
// strictly between Start and Stop (push-to-talk; CLAUDE.md Phase 3) and audio
// is never written anywhere — the engine reads the default microphone live.
// This is the default recognizer; WhisperSpeechToText is the sibling local
// Whisper engine behind the same interface, selectable with --stt whisper.
// -----------------------------------------------------------------------------

using System;
using System.Speech.Recognition;
using System.Text;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>Push-to-talk dictation over <see cref="SpeechRecognitionEngine"/>.</summary>
public sealed class SystemSpeechToText : ISpeechToText
{
    private readonly SpeechRecognitionEngine _engine;
    private readonly StringBuilder _finalText = new();
    private readonly object _gate = new();
    private bool _capturing;
    private bool _disposed;

    /// <summary>Creates the recognizer on the default microphone with a dictation grammar.</summary>
    /// <exception cref="InvalidOperationException">No recognizer or microphone is available.</exception>
    public SystemSpeechToText()
    {
        _engine = new SpeechRecognitionEngine();
        _engine.LoadGrammar(new DictationGrammar());
        _engine.SetInputToDefaultAudioDevice();
        _engine.SpeechHypothesized += OnHypothesized;
        _engine.SpeechRecognized += OnRecognized;
        _engine.RecognizeCompleted += OnRecognizeCompleted;
    }

    /// <inheritdoc/>
    public event EventHandler<string>? PartialRecognized;

    /// <inheritdoc/>
    public event EventHandler<string>? FinalRecognized;

    /// <inheritdoc/>
    public event EventHandler<Exception>? Failed;

    /// <inheritdoc/>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _capturing)
            {
                return;
            }
            _capturing = true;
            _finalText.Clear();
        }
        try
        {
            // Multiple: keep recognizing phrase after phrase until Stop, so a
            // long press captures a full utterance with pauses.
            _engine.RecognizeAsync(RecognizeMode.Multiple);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _capturing = false;
            }
            Failed?.Invoke(this, ex);
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed || !_capturing)
            {
                return;
            }
        }
        try
        {
            // Finishes the in-flight phrase, then raises RecognizeCompleted —
            // which is where the single FinalRecognized is emitted.
            _engine.RecognizeAsyncStop();
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _capturing = false;
            }
            Failed?.Invoke(this, ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
        _engine.SpeechHypothesized -= OnHypothesized;
        _engine.SpeechRecognized -= OnRecognized;
        _engine.RecognizeCompleted -= OnRecognizeCompleted;
        try
        {
            _engine.RecognizeAsyncCancel();
        }
        catch (InvalidOperationException)
        {
            // Not recognizing; nothing to cancel.
        }
        _engine.Dispose();
    }

    // Live hypothesis for the current phrase, shown on the HUD while speaking.
    private void OnHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        string settled;
        lock (_gate)
        {
            if (!_capturing)
            {
                return;
            }
            settled = _finalText.ToString();
        }
        PartialRecognized?.Invoke(this, settled.Length == 0 ? e.Result.Text : $"{settled} {e.Result.Text}");
    }

    // A phrase settled; append it. Multiple phrases accumulate across pauses.
    private void OnRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Result.Text))
        {
            return;
        }
        lock (_gate)
        {
            if (!_capturing)
            {
                return;
            }
            if (_finalText.Length > 0)
            {
                _finalText.Append(' ');
            }
            _finalText.Append(e.Result.Text);
        }
    }

    // Recognition drained after RecognizeAsyncStop: emit exactly one final
    // transcript (empty when nothing was recognized), per the interface contract.
    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        string transcript;
        lock (_gate)
        {
            if (!_capturing)
            {
                return;
            }
            _capturing = false;
            transcript = _finalText.ToString();
        }
        if (e.Error is not null)
        {
            Failed?.Invoke(this, e.Error);
            return;
        }
        FinalRecognized?.Invoke(this, transcript);
    }
}
