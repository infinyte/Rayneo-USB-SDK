// -----------------------------------------------------------------------------
// RayNeoImuSampleTests.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using Infinyte.RayNeo;

namespace RayNeo.Device.Tests;

public sealed class RayNeoImuSampleTests
{
    [Fact]
    public void IsNewerThan_ReturnsFalseWhenTicksMatch()
    {
        var a = new RayNeoImuSample(1, 2, 3, 4, 5, 6, 7, 8, 9, 25f, Tick: 42);
        var b = new RayNeoImuSample(9, 8, 7, 6, 5, 4, 3, 2, 1, 26f, Tick: 42);

        Assert.False(a.IsNewerThan(b));
        Assert.False(b.IsNewerThan(a));
    }

    [Fact]
    public void IsNewerThan_ReturnsTrueWhenTicksDiffer()
    {
        var earlier = new RayNeoImuSample(0, 0, 0, 0, 0, 0, 0, 0, 0, 25f, Tick: 10);
        var later = earlier with { Tick = 11 };
        Assert.True(later.IsNewerThan(earlier));
    }
}
