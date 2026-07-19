// -----------------------------------------------------------------------------
// FakeTimeProvider.cs
// Author: Kurt Mitchell
//
// Deterministic TimeProvider for timer tests: time only moves when Advance is
// called, and due timer callbacks fire synchronously during the advance.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RayNeo.Voice.Tests;

/// <summary>Manual-clock <see cref="TimeProvider"/> for deterministic timer tests.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly List<FakeTimer> _timers = new();
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>UTC, so local-time formatting is deterministic on any machine.</summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state,
            dueTime == Timeout.InfiniteTimeSpan ? null : _now + dueTime);
        lock (_timers)
        {
            _timers.Add(timer);
        }
        return timer;
    }

    /// <summary>Moves the clock forward, firing any timers that come due (in due order).</summary>
    public void Advance(TimeSpan delta)
    {
        DateTimeOffset target = _now + delta;
        while (true)
        {
            FakeTimer? next;
            lock (_timers)
            {
                next = _timers
                    .Where(t => t.DueAt is not null && t.DueAt <= target)
                    .OrderBy(t => t.DueAt)
                    .FirstOrDefault();
            }
            if (next is null)
            {
                break;
            }
            _now = next.DueAt!.Value;
            next.Fire(); // one-shot: clears DueAt before invoking the callback
        }
        _now = target;
    }

    private void Remove(FakeTimer timer)
    {
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class FakeTimer : ITimer
    {
        private readonly FakeTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset? dueAt)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
            DueAt = dueAt;
        }

        public DateTimeOffset? DueAt { get; private set; }

        public void Fire()
        {
            DueAt = null;
            _callback(_state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _owner.GetUtcNow() + dueTime;
            return true;
        }

        public void Dispose() => _owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
