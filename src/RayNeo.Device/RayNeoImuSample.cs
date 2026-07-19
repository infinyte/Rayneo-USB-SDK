// -----------------------------------------------------------------------------
// RayNeoImuSample.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo;

/// <summary>A single decoded IMU sample from the glasses.</summary>
public readonly record struct RayNeoImuSample(
    float AccelX, float AccelY, float AccelZ,          // m/s^2
    float GyroX, float GyroY, float GyroZ,             // deg/s
    float MagX, float MagY, float MagZ,                // uncalibrated
    float TemperatureCelsius,
    uint Tick)                                         // device tick counter
{
    /// <summary>
    /// True when this sample carries new sensor data relative to
    /// <paramref name="previous"/>. The device transmits at ~495 Hz but the
    /// sensor updates more slowly, so consecutive frames repeat values;
    /// the tick field is the reliable deduplication key.
    /// </summary>
    public bool IsNewerThan(in RayNeoImuSample previous) => Tick != previous.Tick;
}
