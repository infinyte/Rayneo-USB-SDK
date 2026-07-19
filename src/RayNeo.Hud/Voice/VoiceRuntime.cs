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
using System.IO;
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

    /// <summary>The speech-to-text backend to build (default: System.Speech).</summary>
    public SpeechEngineKind Engine { get; init; } = SpeechEngineKind.System;

    /// <summary>The ggml model path used when <see cref="Engine"/> is Whisper.</summary>
    public string? WhisperModelPath { get; init; }
}

/// <summary>The assembled voice stack for one HUD session.</summary>
public sealed class VoiceRuntime : IDisposable
{
    private readonly ISpeechToText _speechToText;
    private readonly SystemSpeechSynthesizer _textToSpeech;
    private readonly GlobalPushToTalkHook _pushToTalk;

    private VoiceRuntime(
        VoiceInteractionController controller,
        TimerService timers,
        PinSurface pins,
        ISpeechToText speechToText,
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

        ISpeechToText? speechToText = null;
        SystemSpeechSynthesizer? textToSpeech = null;
        GlobalPushToTalkHook? pushToTalk = null;
        try
        {
            // Build the requested recognizer; a missing/invalid Whisper model
            // degrades to System.Speech with an on-glass warning (never crashes).
            speechToText = CreateSpeechEngine(options, out string? engineWarning);
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

            warning = engineWarning;
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

    // Builds the recognizer named by options.Engine. Whisper degrades to
    // System.Speech (with a warning) when its model is missing or fails to load,
    // so voice stays usable and the wearer can see why (CLAUDE.md Phase 3).
    private static ISpeechToText CreateSpeechEngine(VoiceOptions options, out string? warning)
    {
        warning = null;
        if (options.Engine != SpeechEngineKind.Whisper)
        {
            return new SystemSpeechToText();
        }

        string? modelPath = options.WhisperModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            warning = modelPath is null
                ? "Voice using Windows speech — no Whisper model (set --whisper-model or RAYNEO_WHISPER_MODEL)."
                : $"Voice using Windows speech — Whisper model not found: {modelPath}";
            return new SystemSpeechToText();
        }

        try
        {
            return new WhisperSpeechToText(new WaveInAudioCaptureSource(), new WhisperNetTranscriber(modelPath));
        }
        catch (Exception ex)
        {
            warning = $"Voice using Windows speech — Whisper failed to load: {ex.Message}";
            return new SystemSpeechToText();
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
