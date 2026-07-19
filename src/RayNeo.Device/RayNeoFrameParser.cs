// -----------------------------------------------------------------------------
// RayNeoFrameParser.cs
// Author: Kurt Mitchell
//
// Pure decode helpers for the RayNeo Air vendor HID frames. Extracted from
// RayNeoClient so the wire parsing can be unit-tested without a device.
// -----------------------------------------------------------------------------

using System;
using System.Buffers.Binary;

namespace Infinyte.RayNeo;

/// <summary>Stateless decoders for the 64-byte frames received from the glasses.</summary>
internal static class RayNeoFrameParser
{
    /// <summary>0x99 — start-of-frame byte on every device-to-host report.</summary>
    internal const byte MagicIn = 0x99;

    /// <summary>0x66 — start-of-frame byte on every host-to-device report.</summary>
    internal const byte MagicOut = 0x66;

    /// <summary>Frame type byte for IMU sample reports.</summary>
    internal const byte FrameTypeImu = 0x65;

    /// <summary>Frame type byte for command-ack reports.</summary>
    internal const byte FrameTypeAck = 0xC8;

    /// <summary>Wire size of a single HID frame (excluding the leading report-ID byte).</summary>
    internal const int FrameSize = 64;

    /// <summary>
    /// Decode a 64-byte IMU sample frame (type 0x65). Assumes <paramref name="frame"/>
    /// starts at the 0x99 magic byte and is at least <see cref="FrameSize"/> bytes long.
    /// </summary>
    internal static RayNeoImuSample ParseImuFrame(ReadOnlySpan<byte> frame)
    {
        static float F(ReadOnlySpan<byte> f, int off) =>
            BinaryPrimitives.ReadSingleLittleEndian(f.Slice(off, 4));

        return new RayNeoImuSample(
            AccelX: F(frame, 4), AccelY: F(frame, 8), AccelZ: F(frame, 12),
            GyroX: F(frame, 16), GyroY: F(frame, 20), GyroZ: F(frame, 24),
            MagX: F(frame, 32), MagY: F(frame, 36), MagZ: F(frame, 52),
            TemperatureCelsius: F(frame, 28),
            Tick: BinaryPrimitives.ReadUInt32LittleEndian(frame.Slice(40, 4)));
    }

    /// <summary>Read the acked command ID from a type-0xC8 frame (byte 8).</summary>
    internal static byte ReadAckCommandId(ReadOnlySpan<byte> frame) => frame[8];

    /// <summary>Read the device tick from a type-0xC8 frame (uint32 LE @ 4).</summary>
    internal static uint ReadAckTick(ReadOnlySpan<byte> frame) =>
        BinaryPrimitives.ReadUInt32LittleEndian(frame.Slice(4, 4));

    /// <summary>
    /// Build a 65-byte output report (leading 0x00 report ID + 64-byte payload)
    /// for the host-to-device command layout: 66 | cmd | value | payload | zero pad.
    /// </summary>
    internal static byte[] BuildCommandReport(byte command, byte value, ReadOnlySpan<byte> payload)
    {
        byte[] buffer = new byte[FrameSize + 1];
        buffer[0] = 0x00;          // report ID
        buffer[1] = MagicOut;
        buffer[2] = command;
        buffer[3] = value;
        payload[..Math.Min(payload.Length, 52)].CopyTo(buffer.AsSpan(4));
        return buffer;
    }
}
