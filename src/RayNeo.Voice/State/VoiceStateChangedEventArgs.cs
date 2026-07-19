// -----------------------------------------------------------------------------
// VoiceStateChangedEventArgs.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Voice;

/// <summary>Describes a single accepted transition of the voice state machine.</summary>
public sealed class VoiceStateChangedEventArgs : EventArgs
{
    /// <summary>Creates the event payload for a transition.</summary>
    public VoiceStateChangedEventArgs(VoiceState oldState, VoiceState newState, VoiceTrigger trigger)
    {
        OldState = oldState;
        NewState = newState;
        Trigger = trigger;
    }

    /// <summary>State the machine was in before the trigger.</summary>
    public VoiceState OldState { get; }

    /// <summary>State the machine moved to.</summary>
    public VoiceState NewState { get; }

    /// <summary>Trigger that caused the transition.</summary>
    public VoiceTrigger Trigger { get; }
}
