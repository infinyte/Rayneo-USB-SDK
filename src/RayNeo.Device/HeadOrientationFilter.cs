// -----------------------------------------------------------------------------
// HeadOrientationFilter.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo;

/// <summary>
/// Complementary filter producing head orientation from raw IMU samples.
/// Gyro integration provides responsiveness; the accelerometer's gravity
/// vector corrects pitch/roll drift. Yaw is gyro-only and will drift slowly
/// until magnetometer correction is added (the glasses do report mag data).
/// </summary>
public sealed class HeadOrientationFilter
{
    private const float GyroWeight = 0.98f; // trust gyro short-term, gravity long-term

    private float _pitchDegrees;
    private float _rollDegrees;
    private float _yawDegrees;
    private uint _lastTick;
    private bool _initialized;

    /// <summary>Current head pitch in degrees.</summary>
    public float PitchDegrees => _pitchDegrees;

    /// <summary>Current head roll in degrees.</summary>
    public float RollDegrees => _rollDegrees;

    /// <summary>Current head yaw in degrees (gyro-integrated, drifts over time).</summary>
    public float YawDegrees => _yawDegrees;

    /// <summary>Ticks per second of the device tick counter — calibrate against wall time on first use.</summary>
    public float TickRateHz { get; set; } = 1000f;

    /// <summary>Feed a new IMU sample into the filter.</summary>
    public void Update(in RayNeoImuSample sample)
    {
        if (!_initialized)
        {
            _lastTick = sample.Tick;
            _initialized = true;
            return;
        }

        if (sample.Tick == _lastTick)
        {
            return; // duplicated transport frame; no new sensor data
        }

        float dt = (sample.Tick - _lastTick) / TickRateHz;
        _lastTick = sample.Tick;

        // Gravity-referenced angles from the accelerometer. Axis convention
        // observed on the Air 4 Pro: +Y is up when the glasses are level.
        float accelPitch = MathF.Atan2(sample.AccelZ, sample.AccelY) * (180f / MathF.PI);
        float accelRoll = MathF.Atan2(-sample.AccelX, sample.AccelY) * (180f / MathF.PI);

        // Integrate gyro, then blend toward the gravity reference.
        // NOTE: gyro axis-to-body mapping should be verified empirically
        // (nod / shake / roll test) and adjusted here if needed.
        _pitchDegrees = GyroWeight * (_pitchDegrees + sample.GyroX * dt) + (1f - GyroWeight) * accelPitch;
        _rollDegrees = GyroWeight * (_rollDegrees + sample.GyroZ * dt) + (1f - GyroWeight) * accelRoll;
        _yawDegrees += sample.GyroY * dt;
    }
}
