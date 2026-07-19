// -----------------------------------------------------------------------------
// PcmAudio.cs
// Author: Kurt Mitchell
//
// Converts little-endian 16-bit PCM (as delivered by WaveInEvent) into the
// normalized float samples Whisper expects. Kept as a tiny pure helper so the
// conversion is unit-tested without a microphone. No audio is persisted.
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>16-bit PCM ↔ float conversion helpers.</summary>
public static class PcmAudio
{
    // 1 / 32768: maps the full signed 16-bit range onto [-1, 1).
    private const float Scale = 1f / 32768f;

    /// <summary>
    /// Converts a little-endian 16-bit PCM buffer to floats normalized to
    /// [-1, 1]. Reads <paramref name="count"/> bytes from the front of
    /// <paramref name="buffer"/>; a trailing odd byte (incomplete sample) is
    /// ignored.
    /// </summary>
    /// <param name="buffer">Source PCM bytes, little-endian 16-bit signed.</param>
    /// <param name="count">Number of valid bytes at the start of the buffer.</param>
    /// <returns>One float per complete 16-bit sample.</returns>
    public static float[] ToFloatSamples(byte[] buffer, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count < 0 || count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        int sampleCount = count / 2; // two bytes per sample; drop a trailing odd byte
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short pcm = (short)(buffer[2 * i] | (buffer[2 * i + 1] << 8));
            samples[i] = pcm * Scale;
        }
        return samples;
    }
}
