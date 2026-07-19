// -----------------------------------------------------------------------------
// PcmAudioTests.cs
// Author: Kurt Mitchell
//
// The 16-bit PCM → normalized float conversion: silence, the signed extremes,
// little-endian byte order, and a trailing odd byte being ignored.
// -----------------------------------------------------------------------------

using Infinyte.RayNeo.Hud.Voice;

namespace RayNeo.Hud.Tests;

public sealed class PcmAudioTests
{
    [Fact]
    public void SilenceMapsToZero()
    {
        float[] samples = PcmAudio.ToFloatSamples(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 4);
        Assert.Equal(new[] { 0f, 0f }, samples);
    }

    [Fact]
    public void MaxPositiveMapsToNearOne()
    {
        // short.MaxValue = 32767 → little-endian 0xFF 0x7F.
        float[] samples = PcmAudio.ToFloatSamples(new byte[] { 0xFF, 0x7F }, 2);
        Assert.Single(samples);
        Assert.Equal(32767f / 32768f, samples[0], 5);
    }

    [Fact]
    public void MinNegativeMapsToMinusOne()
    {
        // short.MinValue = -32768 → little-endian 0x00 0x80.
        float[] samples = PcmAudio.ToFloatSamples(new byte[] { 0x00, 0x80 }, 2);
        Assert.Single(samples);
        Assert.Equal(-1f, samples[0], 6);
    }

    [Fact]
    public void ReadsLittleEndian()
    {
        // 0x0102 = 258, stored low byte first.
        float[] samples = PcmAudio.ToFloatSamples(new byte[] { 0x02, 0x01 }, 2);
        Assert.Equal(258f / 32768f, samples[0], 6);
    }

    [Fact]
    public void TrailingOddByteIsIgnored()
    {
        // Three bytes = one complete sample; the dangling third byte is dropped.
        float[] samples = PcmAudio.ToFloatSamples(new byte[] { 0x00, 0x00, 0x7F }, 3);
        Assert.Single(samples);
        Assert.Equal(0f, samples[0]);
    }

    [Fact]
    public void OnlyCountBytesAreConverted()
    {
        // A 100-byte capture buffer with only 4 valid bytes yields 2 samples.
        var buffer = new byte[100];
        float[] samples = PcmAudio.ToFloatSamples(buffer, 4);
        Assert.Equal(2, samples.Length);
    }
}
