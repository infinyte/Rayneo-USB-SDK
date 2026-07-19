// -----------------------------------------------------------------------------
// TimerService.cs
// Author: Kurt Mitchell
//
// Named countdown timers for the voice assistant. Built on TimeProvider so the
// tests drive a fake clock and no real time passes; in production the system
// TimeProvider fires expirations on a timer thread, so consumers (the HUD)
// must marshal to the UI thread themselves.
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>A snapshot of one running timer.</summary>
/// <param name="Name">The timer's spoken name.</param>
/// <param name="Remaining">Time left until it expires (never negative).</param>
public sealed record ActiveTimer(string Name, TimeSpan Remaining);

/// <summary>
/// Manages named countdown timers. Names are case-insensitive and must be
/// unique among *active* timers; an expired or cancelled name is immediately
/// reusable. Thread-safe: tool calls arrive on the assistant loop and
/// expirations on a timer thread.
/// </summary>
public sealed class TimerService : IDisposable
{
    private sealed record Entry(string Name, DateTimeOffset Deadline, ITimer Timer);

    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>Creates the service on the given clock.</summary>
    public TimerService(TimeProvider timeProvider) =>
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Raised with the timer's name when it expires. May fire on a timer thread.</summary>
    public event EventHandler<string>? TimerExpired;

    /// <summary>Active timers, soonest expiry first.</summary>
    public IReadOnlyList<ActiveTimer> ActiveTimers
    {
        get
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            lock (_gate)
            {
                return _entries.Values
                    .OrderBy(e => e.Deadline)
                    .Select(e => new ActiveTimer(e.Name, Max(e.Deadline - now, TimeSpan.Zero)))
                    .ToArray();
            }
        }
    }

    /// <summary>Starts a named countdown.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">A timer with the same name is active.</exception>
    public void StartTimer(string name, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Timer name must be non-empty.", nameof(name));
        }
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Timer duration must be positive.");
        }

        name = name.Trim();
        lock (_gate)
        {
            if (_entries.ContainsKey(name))
            {
                throw new InvalidOperationException($"A timer named '{name}' is already running.");
            }
            ITimer timer = _timeProvider.CreateTimer(
                OnTimerDue, name, duration, Timeout.InfiniteTimeSpan);
            _entries.Add(name, new Entry(name, _timeProvider.GetUtcNow() + duration, timer));
        }
    }

    /// <summary>Cancels a timer by name; false when no such active timer exists.</summary>
    public bool CancelTimer(string name)
    {
        Entry? removed;
        lock (_gate)
        {
            if (!_entries.Remove(name?.Trim() ?? string.Empty, out removed))
            {
                return false;
            }
        }
        removed.Timer.Dispose();
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Entry[] all;
        lock (_gate)
        {
            all = _entries.Values.ToArray();
            _entries.Clear();
        }
        foreach (Entry entry in all)
        {
            entry.Timer.Dispose();
        }
    }

    // Timer callback: remove first so the name is reusable inside the handler,
    // then raise outside the lock so handlers can call back into the service.
    private void OnTimerDue(object? state)
    {
        string name = (string)state!;
        Entry? removed;
        lock (_gate)
        {
            if (!_entries.Remove(name, out removed))
            {
                return; // cancelled in the race window between due and callback
            }
        }
        removed.Timer.Dispose();
        TimerExpired?.Invoke(this, removed.Name);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;
}
