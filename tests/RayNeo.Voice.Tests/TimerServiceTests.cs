// -----------------------------------------------------------------------------
// TimerServiceTests.cs
// Author: Kurt Mitchell
//
// TimerService under a fake clock: start, expire, cancel, list, and input
// validation. No real time passes in any of these tests.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class TimerServiceTests
{
    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public void NewService_HasNoActiveTimers()
    {
        var service = new TimerService(_clock);
        Assert.Empty(service.ActiveTimers);
    }

    [Fact]
    public void StartTimer_AppearsInActiveTimers_WithRemainingTime()
    {
        var service = new TimerService(_clock);
        service.StartTimer("tea", TimeSpan.FromMinutes(3));

        ActiveTimer timer = Assert.Single(service.ActiveTimers);
        Assert.Equal("tea", timer.Name);
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);
    }

    [Fact]
    public void Remaining_CountsDownAsClockAdvances()
    {
        var service = new TimerService(_clock);
        service.StartTimer("tea", TimeSpan.FromMinutes(3));

        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromMinutes(2), Assert.Single(service.ActiveTimers).Remaining);
    }

    [Fact]
    public void ExpiredTimer_RaisesEventOnce_AndLeavesActiveList()
    {
        var service = new TimerService(_clock);
        var fired = new List<string>();
        service.TimerExpired += (_, name) => fired.Add(name);

        service.StartTimer("egg", TimeSpan.FromMinutes(5));
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(new[] { "egg" }, fired);
        Assert.Empty(service.ActiveTimers);

        _clock.Advance(TimeSpan.FromMinutes(10)); // must not re-fire
        Assert.Equal(new[] { "egg" }, fired);
    }

    [Fact]
    public void MultipleTimers_ExpireInDueOrder()
    {
        var service = new TimerService(_clock);
        var fired = new List<string>();
        service.TimerExpired += (_, name) => fired.Add(name);

        service.StartTimer("late", TimeSpan.FromMinutes(10));
        service.StartTimer("early", TimeSpan.FromMinutes(2));
        _clock.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal(new[] { "early", "late" }, fired);
    }

    [Fact]
    public void CancelTimer_RemovesIt_AndPreventsExpiry()
    {
        var service = new TimerService(_clock);
        var fired = new List<string>();
        service.TimerExpired += (_, name) => fired.Add(name);

        service.StartTimer("tea", TimeSpan.FromMinutes(3));
        Assert.True(service.CancelTimer("tea"));
        Assert.Empty(service.ActiveTimers);

        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Empty(fired);
    }

    [Fact]
    public void CancelTimer_UnknownName_ReturnsFalse()
    {
        var service = new TimerService(_clock);
        Assert.False(service.CancelTimer("nope"));
    }

    [Fact]
    public void StartTimer_DuplicateActiveName_Throws()
    {
        var service = new TimerService(_clock);
        service.StartTimer("tea", TimeSpan.FromMinutes(3));
        Assert.Throws<InvalidOperationException>(() => service.StartTimer("tea", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void StartTimer_NameReusableAfterExpiryOrCancel()
    {
        var service = new TimerService(_clock);
        service.StartTimer("tea", TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(2));
        service.StartTimer("tea", TimeSpan.FromMinutes(1)); // expired name reusable

        Assert.True(service.CancelTimer("tea"));
        service.StartTimer("tea", TimeSpan.FromMinutes(1)); // cancelled name reusable
        Assert.Single(service.ActiveTimers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StartTimer_RejectsBlankName(string name)
    {
        var service = new TimerService(_clock);
        Assert.Throws<ArgumentException>(() => service.StartTimer(name, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void StartTimer_RejectsNonPositiveDuration()
    {
        var service = new TimerService(_clock);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.StartTimer("t", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.StartTimer("t", TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void ActiveTimers_OrderedBySoonestFirst()
    {
        var service = new TimerService(_clock);
        service.StartTimer("late", TimeSpan.FromMinutes(10));
        service.StartTimer("early", TimeSpan.FromMinutes(2));

        Assert.Equal(new[] { "early", "late" }, service.ActiveTimers.Select(t => t.Name).ToArray());
    }
}
