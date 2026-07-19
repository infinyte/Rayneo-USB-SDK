// -----------------------------------------------------------------------------
// DeviceOrientationProvider.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using Infinyte.RayNeo;

namespace Infinyte.RayNeo.Hud;

/// <summary>
/// Feeds live IMU samples through <see cref="HeadOrientationFilter"/> and
/// publishes orientation snapshots for the render loop.
/// </summary>
public sealed class DeviceOrientationProvider : IHeadOrientationProvider
{
    private readonly RayNeoClient _client;
    private readonly HeadOrientationFilter _filter = new();
    private RayNeoImuSample _last;                                   // touched only on the HID thread
    private volatile HeadOrientation _current = HeadOrientation.Zero; // published to the render thread

    /// <summary>Wraps an already-opened client.</summary>
    public DeviceOrientationProvider(RayNeoClient client) => _client = client;

    /// <inheritdoc/>
    public HeadOrientation Current => _current;

    /// <inheritdoc/>
    public string StatusText => "GLASSES CONNECTED";

    /// <inheritdoc/>
    public void Start()
    {
        _client.SampleReceived += OnSample;
        _client.EnableImu();
    }

    // Runs on the RayNeoClient reader thread (~495 Hz transport). The filter is
    // owned by this thread alone; the only cross-thread state is the volatile
    // reference below, published with a single atomic assignment. That is the
    // hand-off: no locks, no torn reads.
    private void OnSample(RayNeoImuSample sample)
    {
        if (!sample.IsNewerThan(_last))
        {
            return; // duplicated transport frame
        }
        _last = sample;
        _filter.Update(sample);
        _current = new HeadOrientation(
            _filter.PitchDegrees, _filter.RollDegrees, _filter.YawDegrees,
            isLive: true, isSimulated: false, sample.TemperatureCelsius);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.SampleReceived -= OnSample;
        _client.Dispose(); // disables the IMU stream and closes the HID interface
    }
}
