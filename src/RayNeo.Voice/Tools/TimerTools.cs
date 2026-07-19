// -----------------------------------------------------------------------------
// TimerTools.cs
// Author: Kurt Mitchell
//
// The assistant-facing timer tools over TimerService. Results are short
// factual strings the model repeats back to the wearer.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Text;

namespace Infinyte.RayNeo.Voice;

/// <summary>Factory for the timer tool set (start / cancel / list).</summary>
public static class TimerTools
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromHours(24);

    /// <summary>Creates the <c>start_timer</c> tool.</summary>
    public static IVoiceTool CreateStartTimer(TimerService service) => new DelegateVoiceTool(
        "start_timer",
        "Start a named countdown timer. When it expires the wearer is notified on the " +
        "glasses and by voice. Convert spoken durations to whole seconds.",
        new[]
        {
            new VoiceToolParameter("name", "Short spoken name for the timer, e.g. 'tea'.",
                VoiceToolParameterType.String, IsRequired: true),
            new VoiceToolParameter("seconds", "Duration in seconds (1 to 86400).",
                VoiceToolParameterType.Number, IsRequired: true),
        },
        (args, _) =>
        {
            string name = args.GetRequiredString("name");
            double seconds = args.GetRequiredNumber("seconds");
            if (seconds <= 0 || seconds > MaxDuration.TotalSeconds)
            {
                throw new VoiceToolArgumentException(
                    "Argument 'seconds' must be between 1 and 86400.");
            }
            var duration = TimeSpan.FromSeconds(seconds);
            service.StartTimer(name, duration);
            return Task.FromResult($"Started timer '{name.Trim()}' for {Format(duration)}.");
        });

    /// <summary>Creates the <c>cancel_timer</c> tool.</summary>
    public static IVoiceTool CreateCancelTimer(TimerService service) => new DelegateVoiceTool(
        "cancel_timer",
        "Cancel a running timer by name.",
        new[]
        {
            new VoiceToolParameter("name", "Name of the timer to cancel.",
                VoiceToolParameterType.String, IsRequired: true),
        },
        (args, _) =>
        {
            string name = args.GetRequiredString("name");
            return Task.FromResult(service.CancelTimer(name)
                ? $"Cancelled timer '{name.Trim()}'."
                : $"No active timer named '{name.Trim()}'.");
        });

    /// <summary>Creates the <c>list_timers</c> tool.</summary>
    public static IVoiceTool CreateListTimers(TimerService service) => new DelegateVoiceTool(
        "list_timers",
        "List the running timers and how much time each has left.",
        Array.Empty<VoiceToolParameter>(),
        (_, _) =>
        {
            IReadOnlyList<ActiveTimer> timers = service.ActiveTimers;
            if (timers.Count == 0)
            {
                return Task.FromResult("No active timers.");
            }
            var sb = new StringBuilder("Active timers: ");
            for (int i = 0; i < timers.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("; ");
                }
                sb.Append(CultureInfo.InvariantCulture,
                    $"'{timers[i].Name}' with {Format(timers[i].Remaining)} remaining");
            }
            sb.Append('.');
            return Task.FromResult(sb.ToString());
        });

    // 3 minutes → "3:00"; 90 minutes → "1:30:00". Matches how a person reads a timer.
    private static string Format(TimeSpan t) => t.TotalHours >= 1
        ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
        : $"{t.Minutes}:{t.Seconds:D2}";
}
