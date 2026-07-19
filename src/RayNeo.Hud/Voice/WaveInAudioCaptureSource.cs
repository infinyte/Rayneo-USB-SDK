// -----------------------------------------------------------------------------
// WaveInAudioCaptureSource.cs
// Author: Kurt Mitchell
//
// IAudioCaptureSource over NAudio's WaveInEvent (WinMM). Captures the default
// microphone at 16 kHz / 16-bit / mono — the exact format Whisper expects, so no
// resampling is needed — and converts each buffer to normalized floats in memory.
// Capture runs strictly between Start and Stop; nothing is written to disk.
// -----------------------------------------------------------------------------

using System;
using NAudio.Wave;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>Push-to-talk microphone capture via <see cref="WaveInEvent"/>.</summary>
public sealed class WaveInAudioCaptureSource : IAudioCaptureSource
{
    // 16 kHz / 16-bit / mono: Whisper's native input format (no resampling stage).
    private const int SampleRate = 16_000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    private readonly WaveInEvent _waveIn;
    private readonly object _gate = new();
    private bool _capturing;
    private bool _disposed;

    /// <summary>Creates the capture source on the default recording device.</summary>
    public WaveInAudioCaptureSource()
    {
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
    }

    /// <inheritdoc/>
    public event EventHandler<float[]>? SamplesAvailable;

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
        }
        _waveIn.StartRecording();
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
            _capturing = false;
        }
        _waveIn.StopRecording();
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
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        _waveIn.Dispose();
    }

    // Each captured PCM buffer → normalized floats, forwarded to the orchestrator.
    // The samples live only in this event; they are never persisted.
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        float[] samples = PcmAudio.ToFloatSamples(e.Buffer, e.BytesRecorded);
        if (samples.Length > 0)
        {
            SamplesAvailable?.Invoke(this, samples);
        }
    }

    // WinMM surfaces device faults (unplug, driver error) via the stop event's
    // Exception; forward it so the orchestrator can raise ISpeechToText.Failed.
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Failed?.Invoke(this, e.Exception);
        }
    }
}
