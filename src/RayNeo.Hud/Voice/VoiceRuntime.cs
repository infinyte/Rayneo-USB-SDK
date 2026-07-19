// -----------------------------------------------------------------------------
// VoiceRuntime.cs
// Author: Kurt Mitchell
//
// Composition root for the voice stack: builds the Windows engines, the tool
// registry (timers, session, pins, app launcher), the Claude client, and the
// VoiceInteractionController, and owns their lifetimes. Creation degrades
// gracefully — no API key or no speech support disables voice with an on-HUD
// warning instead of crashing the overlay (CLAUDE.md Phase 3).
// -----------------------------------------------------------------------------

using System;
using System.Windows.Threading;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>Push-to-talk configuration resolved from the command line.</summary>
/// <param name="VirtualKey">Win32 virtual-key code of the hold-to-talk key.</param>
/// <param name="KeyName">Display name shown on the HUD (e.g. "F8").</param>
public sealed record VoiceOptions(int VirtualKey, string KeyName)
{
    /// <summary>F8 — rarely bound by other applications and easy to hold.</summary>
    public static VoiceOptions Default { get; } = new(0x77, "F8");
}

/// <summary>The assembled voice stack for one HUD session.</summary>
public sealed class VoiceRuntime : IDisposable
{
    private readonly SystemSpeechToText _speechToText;
    private readonly SystemSpeechSynthesizer _textToSpeech;
    private readonly GlobalPushToTalkHook _pushToTalk;

    private VoiceRuntime(
        VoiceInteractionController controller,
        TimerService timers,
        PinSurface pins,
        SystemSpeechToText speechToText,
        SystemSpeechSynthesizer textToSpeech,
        GlobalPushToTalkHook pushToTalk,
        VoiceOptions options)
    {
        Controller = controller;
        Timers = timers;
        Pins = pins;
        Options = options;
        _speechToText = speechToText;
        _textToSpeech = textToSpeech;
        _pushToTalk = pushToTalk;
    }

    /// <summary>The running voice loop controller.</summary>
    public VoiceInteractionController Controller { get; }

    /// <summary>Active countdown timers (rendered as HUD chips).</summary>
    public TimerService Timers { get; }

    /// <summary>The world-anchored note pins.</summary>
    public PinSurface Pins { get; }

    /// <summary>The push-to-talk configuration in effect.</summary>
    public VoiceOptions Options { get; }

    /// <summary>
    /// Builds and starts the voice stack, or returns null with a warning when
    /// voice cannot run (missing ANTHROPIC_API_KEY, no speech engine, no mic,
    /// hook failure). Call from the UI thread after the compositor exists.
    /// </summary>
    public static VoiceRuntime? TryCreate(
        Dispatcher dispatcher,
        HudCompositor compositor,
        IHeadOrientationProvider provider,
        VoiceOptions options,
        out string? warning)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            warning = "Voice disabled — set the ANTHROPIC_API_KEY environment variable and restart.";
            return null;
        }

        SystemSpeechToText? speechToText = null;
        SystemSpeechSynthesizer? textToSpeech = null;
        GlobalPushToTalkHook? pushToTalk = null;
        try
        {
            speechToText = new SystemSpeechToText();
            textToSpeech = new SystemSpeechSynthesizer();
            pushToTalk = new GlobalPushToTalkHook(options.VirtualKey);

            var history = new ConversationHistory();
            var timers = new TimerService(TimeProvider.System);
            var pins = new PinSurface(dispatcher, compositor, provider);

            var tools = new VoiceToolRegistry();
            tools.Register(TimerTools.CreateStartTimer(timers));
            tools.Register(TimerTools.CreateCancelTimer(timers));
            tools.Register(TimerTools.CreateListTimers(timers));
            tools.Register(SessionTools.CreateGetCurrentTime(TimeProvider.System));
            tools.Register(SessionTools.CreateSetSpeechMuted(textToSpeech));
            tools.Register(SessionTools.CreateClearConversation(history));
            tools.Register(HudTools.CreatePinNote(pins));
            tools.Register(HudTools.CreateListPins(pins));
            tools.Register(HudTools.CreateClearPins(pins));
            tools.Register(HudTools.CreateOpenAppOrUrl());

            var assistant = new ClaudeAssistantClient(tools: tools);
            var controller = new VoiceInteractionController(
                pushToTalk, speechToText, assistant, textToSpeech, history, timers);
            controller.Start(); // installs the keyboard hook on this (UI) thread

            warning = null;
            return new VoiceRuntime(controller, timers, pins, speechToText, textToSpeech, pushToTalk, options);
        }
        catch (Exception ex)
        {
            speechToText?.Dispose();
            textToSpeech?.Dispose();
            pushToTalk?.Dispose();
            warning = $"Voice disabled — {ex.Message}";
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Controller.Dispose();
        _pushToTalk.Dispose();
        _speechToText.Dispose();
        _textToSpeech.Dispose();
        Timers.Dispose();
    }
}
