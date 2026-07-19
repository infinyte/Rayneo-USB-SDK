// -----------------------------------------------------------------------------
// RayNeoClient.cs
// Author: Kurt Mitchell
//
// Managed client for the RayNeo Air 4 Pro vendor HID protocol.
//
// Protocol summary (verified against a live Air 4 Pro, fw "Jan 10 2026",
// VID 0x1BBB / PID 0xAF50; command + frame layout per the MIT-licensed
// community SDK verncat/RayNeo-Air-3S-Pro-OpenVR):
//
//   Host -> Device (64-byte output report, report ID 0):
//     [0]=0x66  [1]=command  [2]=value  [3..54]=payload  (zero padded)
//
//   Device -> Host (64-byte input report, report ID 0):
//     [0]=0x99  [1]=frame type
//     type 0x65 (IMU sample) — float32 little-endian:
//       accel m/s^2  x/y/z @ 4 / 8 / 12
//       gyro  deg/s  x/y/z @ 16 / 20 / 24
//       temperature °C     @ 28
//       magnetometer x @ 32, y @ 36, z @ 52
//       tick (uint32)      @ 40
//       proximity @ 44, ambient light @ 48
//       Air 4 Pro note: offsets 56-59 and 60-63 carry a duplicated uint32
//       timestamp (NOT the 3S Pro flag/checksum bytes).
//     type 0xC8 (command ack): tick uint32 @ 4, acked command ID @ 8
//
// Dependencies: HidSharp (https://www.nuget.org/packages/HidSharp)
//   dotnet add package HidSharp
// -----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using HidSharp;

[assembly: InternalsVisibleTo("RayNeo.Device.Tests")]

namespace Infinyte.RayNeo;

/// <summary>
/// Connects to RayNeo Air-series glasses over their vendor HID interface,
/// controls the IMU stream, and raises decoded samples.
/// </summary>
public sealed class RayNeoClient : IDisposable
{
    // ---- Protocol constants -------------------------------------------------
    private const int VendorId = 0x1BBB;      // TCL Communication
    private const int ProductId = 0xAF50;     // RayNeo Air 4 Pro (also 3S Pro family)

    private const byte CmdDeviceInfo = 0x00;
    private const byte CmdImuOn = 0x01;
    private const byte CmdImuOff = 0x02;

    // ---- State --------------------------------------------------------------
    private readonly HidDevice _device;
    private readonly HidStream _stream;
    private Thread? _readThread;
    private volatile bool _running;

    /// <summary>Raised for every decoded IMU frame (including repeats; see <see cref="RayNeoImuSample.IsNewerThan"/>).</summary>
    public event Action<RayNeoImuSample>? SampleReceived;

    /// <summary>Raised when the device acknowledges a command (payload byte = command ID).</summary>
    public event Action<byte>? CommandAcknowledged;

    private RayNeoClient(HidDevice device, HidStream stream)
    {
        _device = device;
        _stream = stream;
    }

    /// <summary>
    /// Finds the glasses on the USB bus and opens their vendor HID interface.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the glasses are not connected or the interface cannot be
    /// opened (e.g., held exclusively by vendor software).
    /// </exception>
    public static RayNeoClient Open()
    {
        HidDevice? device = DeviceList.Local.GetHidDeviceOrNull(VendorId, ProductId);
        if (device is null)
        {
            throw new InvalidOperationException(
                "RayNeo glasses not found. Confirm they are plugged in (expected VID 0x1BBB, PID 0xAF50).");
        }

        if (!device.TryOpen(out HidStream stream))
        {
            throw new InvalidOperationException(
                "Found the glasses but could not open the HID interface. Close any RayNeo vendor software and retry.");
        }

        stream.ReadTimeout = Timeout.Infinite;

        var client = new RayNeoClient(device, stream);
        client.StartReadLoop();
        return client;
    }

    /// <summary>Starts the IMU stream (~495 Hz transport rate).</summary>
    public void EnableImu() => SendCommand(CmdImuOn);

    /// <summary>Stops the IMU stream.</summary>
    public void DisableImu() => SendCommand(CmdImuOff);

    /// <summary>Requests a device-info frame (serial, firmware date, settings).</summary>
    public void RequestDeviceInfo() => SendCommand(CmdDeviceInfo);

    /// <summary>Builds and writes a 64-byte command frame: 66 | cmd | value | payload.</summary>
    public void SendCommand(byte command, byte value = 0x00, ReadOnlySpan<byte> payload = default)
    {
        // HidSharp expects a leading report-ID byte; this interface uses
        // numberless reports, so the ID is 0 and the frame follows it.
        byte[] report = RayNeoFrameParser.BuildCommandReport(command, value, payload);
        _stream.Write(report, 0, report.Length);
    }

    // ---- Read loop ----------------------------------------------------------

    private void StartReadLoop()
    {
        _running = true;
        _readThread = new Thread(ReadLoop)
        {
            Name = "RayNeo HID reader",
            IsBackground = true,
        };
        _readThread.Start();
    }

    private void ReadLoop()
    {
        // +1 for the report-ID byte HidSharp prepends on reads.
        byte[] buffer = new byte[RayNeoFrameParser.FrameSize + 1];

        while (_running)
        {
            int count;
            try
            {
                count = _stream.Read(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException) { break; }
            catch (System.IO.IOException) { break; } // device unplugged

            if (count < RayNeoFrameParser.FrameSize)
            {
                continue;
            }

            // Frame starts after the report-ID byte when one is present.
            int offset = count > RayNeoFrameParser.FrameSize ? 1 : 0;
            ReadOnlySpan<byte> frame = buffer.AsSpan(offset, RayNeoFrameParser.FrameSize);
            if (frame[0] != RayNeoFrameParser.MagicIn)
            {
                continue;
            }

            switch (frame[1])
            {
                case RayNeoFrameParser.FrameTypeImu:
                    SampleReceived?.Invoke(RayNeoFrameParser.ParseImuFrame(frame));
                    break;
                case RayNeoFrameParser.FrameTypeAck:
                    CommandAcknowledged?.Invoke(RayNeoFrameParser.ReadAckCommandId(frame));
                    break;
            }
        }
    }

    /// <summary>Stops the read loop and closes the HID stream.</summary>
    public void Dispose()
    {
        _running = false;
        try { DisableImu(); } catch { /* device may already be gone */ }
        _stream.Dispose();
        _readThread?.Join(millisecondsTimeout: 500);
    }
}
