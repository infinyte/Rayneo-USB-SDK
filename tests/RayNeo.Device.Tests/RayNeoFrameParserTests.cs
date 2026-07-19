// -----------------------------------------------------------------------------
// RayNeoFrameParserTests.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo;

namespace RayNeo.Device.Tests;

public sealed class RayNeoFrameParserTests
{
    /// <summary>
    /// Real captured frame from a live Air 4 Pro. Held here as ground truth
    /// for wire decoding — any parser change must decode this identically.
    /// </summary>
    private static readonly byte[] GoldenFrame =
    [
        0x99, 0x65, 0x40, 0x00,
        0x51, 0x17, 0x31, 0x3E, // accelX
        0x57, 0xAA, 0x1F, 0x41, // accelY
        0x30, 0x6A, 0x51, 0x3F, // accelZ
        0xE3, 0x88, 0xE3, 0x3F, // gyroX
        0x44, 0xFA, 0x84, 0xC0, // gyroY
        0x8C, 0x8E, 0xB0, 0xBF, // gyroZ
        0x00, 0x18, 0xFB, 0x41, // temperature
        0x00, 0x46, 0x02, 0xC3, // magX
        0x00, 0xE8, 0x32, 0x42, // magY
        0x44, 0x68, 0xB9, 0x02, // tick
        0x00, 0xB8, 0x09, 0x46, // proximity
        0x00, 0x00, 0x00, 0x00, // ambient
        0x00, 0xA0, 0x8C, 0x42, // magZ
        0x12, 0x68, 0xB9, 0x02, // duplicated timestamp
        0x12, 0x68, 0xB9, 0x02, // duplicated timestamp
    ];

    [Fact]
    public void ParseImuFrame_GoldenVector_DecodesAllFields()
    {
        RayNeoImuSample sample = RayNeoFrameParser.ParseImuFrame(GoldenFrame);

        const float tol = 0.001f;
        Assert.Equal(0.1729f, sample.AccelX, tol);
        Assert.Equal(9.9791f, sample.AccelY, tol);
        Assert.Equal(0.8180f, sample.AccelZ, tol);

        Assert.Equal(1.7776f, sample.GyroX, tol);
        Assert.Equal(-4.1556f, sample.GyroY, tol);
        Assert.Equal(-1.3794f, sample.GyroZ, tol);

        Assert.Equal(31.39f, sample.TemperatureCelsius, 0.01f);

        Assert.Equal(-130.2734f, sample.MagX, tol);
        Assert.Equal(44.7266f, sample.MagY, tol);
        Assert.Equal(70.3125f, sample.MagZ, tol);

        Assert.Equal(45705284u, sample.Tick);
    }

    [Fact]
    public void GoldenFrame_IsFullFrameSize()
    {
        Assert.Equal(RayNeoFrameParser.FrameSize, GoldenFrame.Length);
    }

    [Fact]
    public void BuildCommandReport_EnableImu_HasCorrectPrefixAndZeroPadding()
    {
        byte[] report = RayNeoFrameParser.BuildCommandReport(
            command: 0x01, value: 0x00, payload: ReadOnlySpan<byte>.Empty);

        // Leading report ID (0x00) + 64-byte frame.
        Assert.Equal(65, report.Length);
        Assert.Equal(0x00, report[0]);

        // Frame body: 66 01 00 followed by 61 zero bytes.
        Assert.Equal(0x66, report[1]);
        Assert.Equal(0x01, report[2]);
        Assert.Equal(0x00, report[3]);
        for (int i = 4; i < report.Length; i++)
        {
            Assert.Equal(0, report[i]);
        }
    }

    [Fact]
    public void BuildCommandReport_DisableImu_HasCorrectPrefix()
    {
        byte[] report = RayNeoFrameParser.BuildCommandReport(
            command: 0x02, value: 0x00, payload: ReadOnlySpan<byte>.Empty);

        Assert.Equal(0x00, report[0]);
        Assert.Equal(0x66, report[1]);
        Assert.Equal(0x02, report[2]);
        Assert.Equal(0x00, report[3]);
        for (int i = 4; i < report.Length; i++)
        {
            Assert.Equal(0, report[i]);
        }
    }

    [Fact]
    public void ReadAckCommandId_ReturnsByteAtOffset8()
    {
        Span<byte> frame = stackalloc byte[RayNeoFrameParser.FrameSize];
        frame[0] = 0x99;
        frame[1] = 0xC8;
        frame[8] = 0x2A;
        Assert.Equal((byte)0x2A, RayNeoFrameParser.ReadAckCommandId(frame));
    }
}
