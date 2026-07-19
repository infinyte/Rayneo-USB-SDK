// -----------------------------------------------------------------------------
// WhisperSpeechToText.cs
// Author: Kurt Mitchell
//
// ISpeechToText over a local Whisper recognizer. Whisper has no native streaming
// mode, so this orchestrator accumulates push-to-talk audio in memory and
// produces live hypotheses by periodically re-transcribing the buffer on a
// background task; Stop transcribes the whole buffer once for the final result.
// Capture runs strictly between Start and Stop and the buffer is memory-only and
// cleared every turn — no audio is ever written to disk (CLAUDE.md Phase 3).
//
// The microphone and the Whisper runtime are injected behind IAudioCaptureSource
// and IWhisperTranscriber, so all of the orchestration below unit-tests with
// fakes — no mic, model, or network required.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>Push-to-talk dictation over a local Whisper recognizer.</summary>
public sealed class WhisperSpeechToText : ISpeechToText
{
    // Above ~30 s of held audio, stop launching partial passes: re-transcribing
    // an ever-growing buffer gets expensive and the final pass still covers it.
    private const int MaxPartialSamples = 16_000 * 30;

    private readonly IAudioCaptureSource _capture;
    private readonly IWhisperTranscriber _transcriber;
    private readonly int _partialIntervalSamples;
    private readonly int _minFinalSamples;

    private readonly object _gate = new();
    private readonly List<float> _buffer = new();
    private readonly CancellationTokenSource _lifetime = new();

    private CancellationTokenSource _partialCts = new();
    private bool _capturing;
    private bool _disposed;
    private bool _partialInFlight;
    private int _lastPartialSampleCount;
    private int _generation;

    /// <summary>Creates the orchestrator over an injected microphone and transcriber.</summary>
    /// <param name="capture">Push-to-talk microphone source (ownership is taken).</param>
    /// <param name="transcriber">Whisper transcriber (ownership is taken).</param>
    /// <param name="partialIntervalSamples">
    /// New samples required before another live partial pass runs
    /// (24 000 ≈ 1.5 s at 16 kHz).
    /// </param>
    /// <param name="minFinalSamples">
    /// Below this many buffered samples, Stop emits an empty final transcript
    /// instead of running Whisper on near-silence (4 800 ≈ 0.3 s at 16 kHz).
    /// </param>
    public WhisperSpeechToText(
        IAudioCaptureSource capture,
        IWhisperTranscriber transcriber,
        int partialIntervalSamples = 24_000,
        int minFinalSamples = 4_800)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(transcriber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partialIntervalSamples);
        ArgumentOutOfRangeException.ThrowIfNegative(minFinalSamples);

        _capture = capture;
        _transcriber = transcriber;
        _partialIntervalSamples = partialIntervalSamples;
        _minFinalSamples = minFinalSamples;

        _capture.SamplesAvailable += OnSamplesAvailable;
        _capture.Failed += OnCaptureFailed;
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
        CancellationTokenSource? previous;
        lock (_gate)
        {
            if (_disposed || _capturing)
            {
                return;
            }
            _capturing = true;
            _buffer.Clear();
            _lastPartialSampleCount = 0;
            _partialInFlight = false;
            _generation++;

            // Fresh cancellation scope; abandon any stragglers from the last turn.
            previous = _partialCts;
            _partialCts = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();

        try
        {
            _capture.Start();
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
        float[]? finalSamples;
        bool emitEmpty;
        CancellationTokenSource partialToCancel;
        CancellationToken finalToken;
        lock (_gate)
        {
            if (_disposed || !_capturing)
            {
                return;
            }
            _capturing = false;
            partialToCancel = _partialCts;
            finalToken = _lifetime.Token;

            if (_buffer.Count < _minFinalSamples)
            {
                // Near-silence: skip Whisper (it hallucinates on empty audio) and
                // honor the contract's empty-string final directly.
                finalSamples = null;
                emitEmpty = true;
            }
            else
            {
                finalSamples = _buffer.ToArray();
                emitEmpty = false;
            }
        }

        // Cancel any in-flight partial so a stale hypothesis cannot arrive after Stop.
        partialToCancel.Cancel();

        try
        {
            _capture.Stop();
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, ex);
            return;
        }

        if (emitEmpty)
        {
            FinalRecognized?.Invoke(this, string.Empty);
            return;
        }

        _ = RunFinalAsync(finalSamples!, finalToken);
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
            _capturing = false;
        }

        _partialCts.Cancel();
        _lifetime.Cancel();

        _capture.SamplesAvailable -= OnSamplesAvailable;
        _capture.Failed -= OnCaptureFailed;
        try
        {
            _capture.Stop();
        }
        catch
        {
            // Best-effort on teardown; the device is being released regardless.
        }

        _capture.Dispose();
        _transcriber.Dispose();
        _partialCts.Dispose();
        _lifetime.Dispose();
    }

    // Accumulate captured audio and, once enough new audio has arrived and no
    // pass is already running, launch one live re-transcription of the buffer.
    private void OnSamplesAvailable(object? sender, float[] samples)
    {
        if (samples is null || samples.Length == 0)
        {
            return;
        }

        float[]? snapshot = null;
        CancellationToken token = default;
        int generation = 0;
        lock (_gate)
        {
            if (_disposed || !_capturing)
            {
                return;
            }
            _buffer.AddRange(samples);

            if (!_partialInFlight
                && _buffer.Count - _lastPartialSampleCount >= _partialIntervalSamples
                && _buffer.Count <= MaxPartialSamples)
            {
                _partialInFlight = true;
                _lastPartialSampleCount = _buffer.Count;
                snapshot = _buffer.ToArray();
                token = _partialCts.Token;
                generation = _generation;
            }
        }

        if (snapshot is not null)
        {
            _ = RunPartialAsync(snapshot, generation, token);
        }
    }

    // One live hypothesis pass. Best-effort: transcription errors (including
    // cancellation when the turn ends) are swallowed — only the final pass faults.
    private async Task RunPartialAsync(float[] samples, int generation, CancellationToken token)
    {
        string text;
        try
        {
            text = await _transcriber.TranscribeAsync(samples, token).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _partialInFlight = false;
            }
            return;
        }

        string? emit = null;
        lock (_gate)
        {
            _partialInFlight = false;
            // Suppress a hypothesis that finished after Stop or after a new turn began.
            if (!_disposed && _capturing && generation == _generation)
            {
                emit = text;
            }
        }

        if (emit is not null)
        {
            PartialRecognized?.Invoke(this, emit);
        }
    }

    // The single final transcription after Stop: exactly one FinalRecognized, or
    // one Failed on error — never both, matching SystemSpeechToText.
    private async Task RunFinalAsync(float[] samples, CancellationToken token)
    {
        string text;
        try
        {
            text = await _transcriber.TranscribeAsync(samples, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Disposed mid-transcription; the turn is abandoned, so raise nothing.
            return;
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, ex);
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        FinalRecognized?.Invoke(this, text ?? string.Empty);
    }

    // The microphone faulted (unplugged, driver error): surface it and reset so
    // the next push-to-talk turn can start cleanly.
    private void OnCaptureFailed(object? sender, Exception ex)
    {
        lock (_gate)
        {
            if (_disposed || !_capturing)
            {
                return;
            }
            _capturing = false;
        }
        Failed?.Invoke(this, ex);
    }
}
