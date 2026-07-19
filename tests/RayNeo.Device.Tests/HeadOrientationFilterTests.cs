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
    public void DefaultTickRate_IsCalibratedTenKilohertz()
    {
        // Measured against wall time on a live Air 4 Pro (calibrate): three runs
        // read 10001 / 10002 / 10004 Hz — a clean 10 kHz counter.
        var filter = new HeadOrientationFilter();
        Assert.Equal(10000f, filter.TickRateHz);
    }

    [Fact]
    public void LookingUp_ConvergesToPositivePitch()
    {
        // Sign convention verified live: looking up must read positive pitch.
        // A look-up hold rotates gravity by 20° about the pitch axis; the
        // observed device frame has AccelZ negative when the chin lifts.
        var filter = new HeadOrientationFilter { TickRateHz = 10000f };
        float rad = 20f * (MathF.PI / 180f);
        float accelY = Gravity * MathF.Cos(rad);
        float accelZ = -Gravity * MathF.Sin(rad);

        for (uint i = 0; i < 500; i++)
        {
            filter.Update(new RayNeoImuSample(
                AccelX: 0f, AccelY: accelY, AccelZ: accelZ,
                GyroX: 0f, GyroY: 0f, GyroZ: 0f,
                MagX: 0f, MagY: 0f, MagZ: 0f,
                TemperatureCelsius: 25f,
                Tick: i * 10u));
        }

        Assert.Equal(20f, filter.PitchDegrees, 0.05f);
    }

    [Fact]
    public void TiltingRight_ConvergesToPositiveRoll()
    {
        // Sign convention verified live: tilting the head right (right ear to
        // shoulder) must read positive roll. Gravity rotates 15° about roll;
        // the observed device frame has AccelX negative in a right tilt.
        var filter = new HeadOrientationFilter { TickRateHz = 10000f };
        float rad = 15f * (MathF.PI / 180f);
        float accelX = -Gravity * MathF.Sin(rad);
        float accelY = Gravity * MathF.Cos(rad);

        for (uint i = 0; i < 500; i++)
        {
            filter.Update(new RayNeoImuSample(
                AccelX: accelX, AccelY: accelY, AccelZ: 0f,
                GyroX: 0f, GyroY: 0f, GyroZ: 0f,
                MagX: 0f, MagY: 0f, MagZ: 0f,
                TemperatureCelsius: 25f,
                Tick: i * 10u));
        }

        Assert.Equal(15f, filter.RollDegrees, 0.05f);
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
