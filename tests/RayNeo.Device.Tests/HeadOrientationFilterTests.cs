// -----------------------------------------------------------------------------
// HeadOrientationFilterTests.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;
using Infinyte.RayNeo;

namespace RayNeo.Device.Tests;

public sealed class HeadOrientationFilterTests
{
    private const float Gravity = 9.81f;

    [Fact]
    public void AtRest_ConvergesPitchAndRollToZero()
    {
        var filter = new HeadOrientationFilter { TickRateHz = 1000f };

        // Feed 500 at-rest samples. Accel points along +Y (gravity), gyro zero.
        // Tick advances by 10 per sample (1 ms/step at 1000 Hz).
        for (uint i = 0; i < 500; i++)
        {
            var sample = new RayNeoImuSample(
                AccelX: 0f, AccelY: Gravity, AccelZ: 0f,
                GyroX: 0f, GyroY: 0f, GyroZ: 0f,
                MagX: 0f, MagY: 0f, MagZ: 0f,
                TemperatureCelsius: 25f,
                Tick: i * 10u);
            filter.Update(sample);
        }

        Assert.InRange(filter.PitchDegrees, -0.01f, 0.01f);
        Assert.InRange(filter.RollDegrees, -0.01f, 0.01f);
    }

    [Fact]
    public void ConstantYawRate_IntegratesToRateTimesElapsed()
    {
        const float tickRateHz = 1000f;
        const float yawRateDps = 30f;

        var filter = new HeadOrientationFilter { TickRateHz = tickRateHz };

        // 101 samples with ticks 0, 10, ..., 1000 → total elapsed = 1000/1000 = 1 s.
        // First sample only initializes the tick reference; subsequent 100 samples
        // each integrate 0.01 s of yaw motion.
        const uint tickStep = 10u;
        const int samples = 101;
        for (int i = 0; i < samples; i++)
        {
            var sample = new RayNeoImuSample(
                AccelX: 0f, AccelY: Gravity, AccelZ: 0f,
                GyroX: 0f, GyroY: yawRateDps, GyroZ: 0f,
                MagX: 0f, MagY: 0f, MagZ: 0f,
                TemperatureCelsius: 25f,
                Tick: (uint)i * tickStep);
            filter.Update(sample);
        }

        float expectedYaw = yawRateDps * ((samples - 1) * tickStep / tickRateHz);
        Assert.Equal(expectedYaw, filter.YawDegrees, 0.001f);
    }
}
