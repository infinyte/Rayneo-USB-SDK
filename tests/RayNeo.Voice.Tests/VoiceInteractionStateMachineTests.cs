// -----------------------------------------------------------------------------
// VoiceInteractionStateMachineTests.cs
// Author: Kurt Mitchell
//
// Exhaustive coverage of the voice interaction state machine: every legal
// transition, every illegal (state, trigger) pair, the barge-in paths, and the
// full happy-path cycle. No audio, network, or UI is involved (CLAUDE.md Phase 3).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class VoiceInteractionStateMachineTests
{
    // The authoritative legal-transition table, duplicated here independently of
    // the production table so the two cross-check each other.
    private static readonly Dictionary<(VoiceState, VoiceTrigger), VoiceState> Legal = new()
    {
        [(VoiceState.Idle, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,

        [(VoiceState.Listening, VoiceTrigger.PushToTalkReleased)] = VoiceState.Transcribing,
        [(VoiceState.Listening, VoiceTrigger.Fault)] = VoiceState.Idle,

        [(VoiceState.Transcribing, VoiceTrigger.TranscriptRecognized)] = VoiceState.Thinking,
        [(VoiceState.Transcribing, VoiceTrigger.TranscriptEmpty)] = VoiceState.Idle,
        [(VoiceState.Transcribing, VoiceTrigger.Fault)] = VoiceState.Idle,

        [(VoiceState.Thinking, VoiceTrigger.ResponseStarted)] = VoiceState.Streaming,
        [(VoiceState.Thinking, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,
        [(VoiceState.Thinking, VoiceTrigger.Fault)] = VoiceState.Idle,

        [(VoiceState.Streaming, VoiceTrigger.ResponseCompletedWithSpeech)] = VoiceState.Speaking,
        [(VoiceState.Streaming, VoiceTrigger.ResponseCompletedSilently)] = VoiceState.Idle,
        [(VoiceState.Streaming, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,
        [(VoiceState.Streaming, VoiceTrigger.Fault)] = VoiceState.Idle,

        [(VoiceState.Speaking, VoiceTrigger.SpeechCompleted)] = VoiceState.Idle,
        [(VoiceState.Speaking, VoiceTrigger.PushToTalkPressed)] = VoiceState.Listening,
        [(VoiceState.Speaking, VoiceTrigger.Fault)] = VoiceState.Idle,
    };

    [Fact]
    public void StartsIdle()
    {
        Assert.Equal(VoiceState.Idle, new VoiceInteractionStateMachine().CurrentState);
    }

    [Fact]
    public void EveryLegalTransition_MovesToExpectedState_AndRaisesEvent()
    {
        foreach (((VoiceState from, VoiceTrigger trigger), VoiceState to) in Legal)
        {
            var machine = new VoiceInteractionStateMachine();
            Drive(machine, from);

            VoiceStateChangedEventArgs? captured = null;
            machine.StateChanged += (_, e) => captured = e;

            Assert.True(machine.CanFire(trigger), $"CanFire({from},{trigger})");
            VoiceState result = machine.Fire(trigger);

            Assert.Equal(to, result);
            Assert.Equal(to, machine.CurrentState);
            Assert.NotNull(captured);
            Assert.Equal(from, captured!.OldState);
            Assert.Equal(to, captured.NewState);
            Assert.Equal(trigger, captured.Trigger);
        }
    }

    [Fact]
    public void EveryIllegalTransition_IsRejected_WithoutChangingStateOrRaisingEvent()
    {
        foreach (VoiceState from in Enum.GetValues<VoiceState>())
        {
            foreach (VoiceTrigger trigger in Enum.GetValues<VoiceTrigger>())
            {
                if (Legal.ContainsKey((from, trigger)))
                {
                    continue;
                }

                var machine = new VoiceInteractionStateMachine();
                Drive(machine, from);

                bool eventRaised = false;
                machine.StateChanged += (_, _) => eventRaised = true;

                Assert.False(machine.CanFire(trigger), $"CanFire({from},{trigger}) should be false");

                Assert.False(machine.TryFire(trigger, out VoiceState after));
                Assert.Equal(from, after);
                Assert.Equal(from, machine.CurrentState);

                Assert.Throws<InvalidOperationException>(() => machine.Fire(trigger));
                Assert.Equal(from, machine.CurrentState);

                Assert.False(eventRaised, $"illegal ({from},{trigger}) must not raise StateChanged");
            }
        }
    }

    [Fact]
    public void HappyPath_CyclesBackToIdle()
    {
        var machine = new VoiceInteractionStateMachine();

        Assert.Equal(VoiceState.Listening, machine.Fire(VoiceTrigger.PushToTalkPressed));
        Assert.Equal(VoiceState.Transcribing, machine.Fire(VoiceTrigger.PushToTalkReleased));
        Assert.Equal(VoiceState.Thinking, machine.Fire(VoiceTrigger.TranscriptRecognized));
        Assert.Equal(VoiceState.Streaming, machine.Fire(VoiceTrigger.ResponseStarted));
        Assert.Equal(VoiceState.Speaking, machine.Fire(VoiceTrigger.ResponseCompletedWithSpeech));
        Assert.Equal(VoiceState.Idle, machine.Fire(VoiceTrigger.SpeechCompleted));
    }

    [Fact]
    public void SilentCompletion_SkipsSpeakingAndReturnsToIdle()
    {
        var machine = new VoiceInteractionStateMachine();
        Drive(machine, VoiceState.Streaming);

        Assert.Equal(VoiceState.Idle, machine.Fire(VoiceTrigger.ResponseCompletedSilently));
    }

    [Fact]
    public void EmptyTranscript_ReturnsToIdle()
    {
        var machine = new VoiceInteractionStateMachine();
        Drive(machine, VoiceState.Transcribing);

        Assert.Equal(VoiceState.Idle, machine.Fire(VoiceTrigger.TranscriptEmpty));
    }

    [Theory]
    [InlineData(VoiceState.Thinking)]
    [InlineData(VoiceState.Streaming)]
    [InlineData(VoiceState.Speaking)]
    public void PushToTalk_BargesInFromActiveStates(VoiceState from)
    {
        var machine = new VoiceInteractionStateMachine();
        Drive(machine, from);

        Assert.Equal(VoiceState.Listening, machine.Fire(VoiceTrigger.PushToTalkPressed));
    }

    [Fact]
    public void TryFire_ReturnsTrue_AndReportsNewState_OnLegalTrigger()
    {
        var machine = new VoiceInteractionStateMachine();

        Assert.True(machine.TryFire(VoiceTrigger.PushToTalkPressed, out VoiceState state));
        Assert.Equal(VoiceState.Listening, state);
        Assert.Equal(VoiceState.Listening, machine.CurrentState);
    }

    // Drives the machine from Idle to the requested state along a known path.
    private static void Drive(VoiceInteractionStateMachine machine, VoiceState target)
    {
        switch (target)
        {
            case VoiceState.Idle:
                return;
            case VoiceState.Listening:
                machine.Fire(VoiceTrigger.PushToTalkPressed);
                return;
            case VoiceState.Transcribing:
                machine.Fire(VoiceTrigger.PushToTalkPressed);
                machine.Fire(VoiceTrigger.PushToTalkReleased);
                return;
            case VoiceState.Thinking:
                Drive(machine, VoiceState.Transcribing);
                machine.Fire(VoiceTrigger.TranscriptRecognized);
                return;
            case VoiceState.Streaming:
                Drive(machine, VoiceState.Thinking);
                machine.Fire(VoiceTrigger.ResponseStarted);
                return;
            case VoiceState.Speaking:
                Drive(machine, VoiceState.Streaming);
                machine.Fire(VoiceTrigger.ResponseCompletedWithSpeech);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }
}
