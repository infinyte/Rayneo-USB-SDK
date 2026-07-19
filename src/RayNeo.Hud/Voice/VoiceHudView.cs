// -----------------------------------------------------------------------------
// VoiceHudView.cs
// Author: Kurt Mitchell
//
// The on-glass voice UI: state indicator, live transcript / streaming reply
// panel, tool-activity toast, and timer chips. Controller events arrive on
// arbitrary threads (hook, recognizer, streaming task, timers), so handlers
// only write volatile snapshot fields; the per-frame callbacks — which always
// run on the UI thread — read those snapshots and update the visuals. Every
// voice state change is therefore visible on the glasses within one frame
// (CLAUDE.md Phase 3: Kurt cannot see the console while wearing them).
// -----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>Builds and drives the voice-related HUD elements.</summary>
public sealed class VoiceHudView
{
    private static readonly TimeSpan ToastLifetime = TimeSpan.FromSeconds(4);

    private readonly VoiceRuntime _runtime;

    // Snapshot state written by event handlers (any thread), read per frame (UI
    // thread). Strings are immutable, so a torn read is impossible.
    private volatile string _panelText = string.Empty;
    private volatile string _toastText = string.Empty;
    private DateTime _toastShownAtUtc;
    private readonly StringBuilder _reply = new();
    private readonly object _gate = new();

    private VoiceHudView(VoiceRuntime runtime) => _runtime = runtime;

    /// <summary>Creates the voice UI on <paramref name="compositor"/> and wires it to the runtime.</summary>
    public static VoiceHudView Attach(HudCompositor compositor, VoiceRuntime runtime)
    {
        var view = new VoiceHudView(runtime);
        view.Subscribe(runtime.Controller);
        view.BuildElements(compositor);
        return view;
    }

    // ---- Event handlers (any thread; snapshot writes only) ------------------

    private void Subscribe(VoiceInteractionController controller)
    {
        controller.StateChanged += (_, e) =>
        {
            if (e.NewState == VoiceState.Listening)
            {
                lock (_gate)
                {
                    _reply.Clear();
                }
                _panelText = string.Empty;
            }
        };
        controller.PartialTranscript += (_, partial) => _panelText = "… " + partial;
        controller.ReplyDelta += (_, delta) =>
        {
            lock (_gate)
            {
                _reply.Append(delta);
                _panelText = _reply.ToString();
            }
        };
        controller.ToolActivity += (_, e) => ShowToast(e.Status switch
        {
            ToolActivityStatus.Started => $"⚙ {e.ToolName}…",
            ToolActivityStatus.Succeeded => $"⚙ {e.ToolName} ✓",
            _ => $"⚙ {e.ToolName} ✗",
        });
        controller.TimerAnnouncement += (_, announcement) => ShowToast("⏰ " + announcement);
        controller.ErrorOccurred += (_, message) => _panelText = "⚠ " + message;
    }

    private void ShowToast(string text)
    {
        _toastText = text;
        _toastShownAtUtc = DateTime.UtcNow;
    }

    // ---- Elements (built and updated on the UI thread) ----------------------

    private void BuildElements(HudCompositor compositor)
    {
        // Voice state indicator, bottom-right: colour-coded dot + state name.
        TextBlock state = MakeText(18, FontWeights.SemiBold, Brushes.Cyan);
        compositor.Add(new ScreenFixedElement(state, ScreenAnchor.BottomRight, margin: 24,
            onFrame: _ =>
            {
                VoiceState current = _runtime.Controller.CurrentState;
                (string label, Brush brush) = current switch
                {
                    VoiceState.Idle => ($"● IDLE — hold {_runtime.Options.KeyName} to talk", Brushes.Gray),
                    VoiceState.Listening => ("● LISTENING", Brushes.OrangeRed),
                    VoiceState.Transcribing => ("● TRANSCRIBING", Brushes.Orange),
                    VoiceState.Thinking => ("● THINKING", Brushes.Gold),
                    VoiceState.Streaming => ("● STREAMING", Brushes.Cyan),
                    _ => ("● SPEAKING", Brushes.LightGreen),
                };
                state.Text = label;
                state.Foreground = brush;
            }));

        // Transcript / reply panel, bottom-centre above the orientation readout.
        TextBlock panel = MakeText(20, FontWeights.Normal, Brushes.White);
        panel.TextWrapping = TextWrapping.Wrap;
        panel.TextAlignment = TextAlignment.Center;
        panel.MaxWidth = 760;
        compositor.Add(new ScreenFixedElement(panel, ScreenAnchor.BottomCenter, margin: 72,
            onFrame: _ => panel.Text = _panelText));

        // Tool-activity / timer toast, top-right, auto-hiding after a few seconds.
        TextBlock toast = MakeText(17, FontWeights.SemiBold, Brushes.Gold);
        compositor.Add(new ScreenFixedElement(toast, ScreenAnchor.TopRight, margin: 24,
            onFrame: _ => toast.Text =
                DateTime.UtcNow - _toastShownAtUtc < ToastLifetime ? _toastText : string.Empty));

        // Timer chips, top-right under the toast: "tea 2:41  ·  egg 0:12".
        TextBlock chips = MakeText(16, FontWeights.Normal, Brushes.White);
        compositor.Add(new ScreenFixedElement(chips, ScreenAnchor.TopRight, margin: 56,
            onFrame: _ =>
            {
                var timers = _runtime.Timers.ActiveTimers;
                chips.Text = timers.Count == 0
                    ? string.Empty
                    : string.Join("  ·  ", timers.Select(t =>
                        $"⏱ {t.Name} {(int)t.Remaining.TotalMinutes}:{t.Remaining.Seconds:D2}"));
            }));
    }

    private static TextBlock MakeText(double size, FontWeight weight, Brush brush) => new()
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = size,
        FontWeight = weight,
        Foreground = brush,
        IsHitTestVisible = false,
        Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 },
    };
}
