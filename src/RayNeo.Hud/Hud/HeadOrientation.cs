// -----------------------------------------------------------------------------
// HeadOrientation.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Hud;

/// <summary>
/// Immutable snapshot of head orientation handed from the sample thread to the
/// render loop. Immutability is deliberate: the producer publishes a new
/// instance with a single reference assignment, so the consumer never sees a
/// torn read (see <see cref="DeviceOrientationProvider"/>).
/// </summary>
public sealed class HeadOrientation
{
    /// <summary>Head pitch in degrees (looking up is positive).</summary>
    public float PitchDegrees { get; }

    /// <summary>Head roll in degrees (tilting right is positive).</summary>
    public float RollDegrees { get; }

    /// <summary>Head yaw in degrees (gyro-integrated; drifts over time).</summary>
    public float YawDegrees { get; }

    /// <summary>True when the data comes from connected hardware.</summary>
    public bool IsLive { get; }

    /// <summary>True when the data is synthetic (no glasses attached).</summary>
    public bool IsSimulated { get; }

    /// <summary>Last reported die temperature in °C (0 when simulated).</summary>
    public float TemperatureCelsius { get; }

    /// <summary>Creates an orientation snapshot.</summary>
    public HeadOrientation(float pitch, float roll, float yaw, bool isLive, bool isSimulated, float temperatureCelsius)
    {
        PitchDegrees = pitch;
        RollDegrees = roll;
        YawDegrees = yaw;
        IsLive = isLive;
        IsSimulated = isSimulated;
        TemperatureCelsius = temperatureCelsius;
    }

    /// <summary>A neutral, disconnected orientation.</summary>
    public static readonly HeadOrientation Zero = new(0f, 0f, 0f, false, false, 0f);
}
