// -----------------------------------------------------------------------------
// VoiceState.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// The states of the hands-free voice loop. The nominal happy-path cycle is
/// Idle → Listening → Transcribing → Thinking → Streaming → Speaking → Idle.
/// </summary>
public enum VoiceState
{
    /// <summary>Nothing in flight; waiting for push-to-talk.</summary>
    Idle,

    /// <summary>Push-to-talk held; microphone capturing and producing partial text.</summary>
    Listening,

    /// <summary>Push-to-talk released; recognizer finalizing the transcript.</summary>
    Transcribing,

    /// <summary>Transcript sent to the assistant; awaiting the first token.</summary>
    Thinking,

    /// <summary>Assistant reply streaming in.</summary>
    Streaming,

    /// <summary>Reply complete; text-to-speech reading it aloud.</summary>
    Speaking,
}
