// -----------------------------------------------------------------------------
// VoiceInteractionStateMachine.cs
// Author: Kurt Mitchell
//
// The formal interaction state machine for the hands-free voice loop. It is a
// pure, total transition table with no dependency on audio, networking, or UI,
// so every legal and illegal transition is unit-testable (CLAUDE.md Phase 3).
// The controller in RayNeo.Hud drives it from the hotkey, recognizer, assistant
// client, and synthesizer, and mirrors CurrentState onto the glasses.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// Deterministic state machine for the voice loop
/// (Idle → Listening → Transcribing → Thinking → Streaming → Speaking → Idle),
/// with push-to-talk barge-in and a fault path back to Idle from any active state.
/// </summary>
public sealed class VoiceInteractionStateMachine
{
    // The complete set of legal transitions. Any (state, trigger) pair absent
    // from this map is illegal: Fire throws and TryFire returns false. Keeping
    // the table explicit (rather than switch statements) makes the legal/illegal
    // surface exhaustively enumerable from the tests.
    private static readonly IReadOnlyDictionary<(VoiceState, VoiceTrigger), VoiceState> Transitions =
        new Dictionary<(VoiceState, VoiceTrigger), VoiceState>
        {
            // Idle: the only way forward is to hold push-to-talk.
            [(VoiceState.Idle, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,

            // Listening: release to finalize, or fault out.
            [(VoiceState.Listening, VoiceTrigger.PushToTalkReleased)] = VoiceState.Transcribing,
            [(VoiceState.Listening, VoiceTrigger.Fault)] = VoiceState.Idle,

            // Transcribing: recognized text advances to Thinking; empty returns to Idle.
            [(VoiceState.Transcribing, VoiceTrigger.TranscriptRecognized)] = VoiceState.Thinking,
            [(VoiceState.Transcribing, VoiceTrigger.TranscriptEmpty)] = VoiceState.Idle,
            [(VoiceState.Transcribing, VoiceTrigger.Fault)] = VoiceState.Idle,

            // Thinking: first token streams; barge-in abandons the request; fault out.
            [(VoiceState.Thinking, VoiceTrigger.ResponseStarted)] = VoiceState.Streaming,
            [(VoiceState.Thinking, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,
            [(VoiceState.Thinking, VoiceTrigger.Fault)] = VoiceState.Idle,

            // Streaming: complete into Speaking or straight to Idle; barge-in; fault.
            [(VoiceState.Streaming, VoiceTrigger.ResponseCompletedWithSpeech)] = VoiceState.Speaking,
            [(VoiceState.Streaming, VoiceTrigger.ResponseCompletedSilently)] = VoiceState.Idle,
            [(VoiceState.Streaming, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,
            [(VoiceState.Streaming, VoiceTrigger.Fault)] = VoiceState.Idle,

            // Speaking: finish naturally, or barge in to interrupt the spoken reply.
            [(VoiceState.Speaking, VoiceTrigger.SpeechCompleted)] = VoiceState.Idle,
            [(VoiceState.Speaking, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,
            [(VoiceState.Speaking, VoiceTrigger.Fault)] = VoiceState.Idle,
        };

    /// <summary>The machine's current state. Starts at <see cref="VoiceState.Idle"/>.</summary>
    public VoiceState CurrentState { get; private set; } = VoiceState.Idle;

    /// <summary>Raised after every accepted transition (never for a rejected trigger).</summary>
    public event EventHandler<VoiceStateChangedEventArgs>? StateChanged;

    /// <summary>True when <paramref name="trigger"/> is legal from the current state.</summary>
    public bool CanFire(VoiceTrigger trigger) =>
        Transitions.ContainsKey((CurrentState, trigger));

    /// <summary>
    /// Applies <paramref name="trigger"/>, moving to the next state and raising
    /// <see cref="StateChanged"/>.
    /// </summary>
    /// <returns>The state after the transition.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the trigger is not legal from the current state.
    /// </exception>
    public VoiceState Fire(VoiceTrigger trigger)
    {
        if (!Transitions.TryGetValue((CurrentState, trigger), out VoiceState next))
        {
            throw new InvalidOperationException(
                $"Trigger '{trigger}' is not valid from state '{CurrentState}'.");
        }

        VoiceState previous = CurrentState;
        CurrentState = next;
        StateChanged?.Invoke(this, new VoiceStateChangedEventArgs(previous, next, trigger));
        return next;
    }

    /// <summary>
    /// Attempts <paramref name="trigger"/>. On success transitions and reports the
    /// new state; on an illegal trigger leaves the state unchanged and returns false.
    /// The controller uses this so a stray input (e.g. a second key press) is a
    /// no-op rather than a crash.
    /// </summary>
    public bool TryFire(VoiceTrigger trigger, out VoiceState state)
    {
        if (!Transitions.TryGetValue((CurrentState, trigger), out VoiceState next))
        {
            state = CurrentState;
            return false;
        }

        VoiceState previous = CurrentState;
        CurrentState = next;
        state = next;
        StateChanged?.Invoke(this, new VoiceStateChangedEventArgs(previous, next, trigger));
        return true;
    }
}
