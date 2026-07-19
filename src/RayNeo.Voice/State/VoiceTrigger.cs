// -----------------------------------------------------------------------------
// VoiceTrigger.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// Inputs that drive the voice loop. Each trigger is a pure signal — data-
/// dependent branches (empty transcript, TTS muted) are modelled as distinct
/// triggers so <see cref="VoiceInteractionStateMachine"/> stays a total,
/// side-effect-free transition table that can be exhaustively unit-tested.
/// </summary>
public enum VoiceTrigger
{
    /// <summary>Push-to-talk pressed. Starts listening, or barges in on an in-flight reply.</summary>
    PushToTalkPressed,

    /// <summary>Push-to-talk released. Ends capture and begins finalizing the transcript.</summary>
    PushToTalkReleased,

    /// <summary>Recognizer produced a non-empty final transcript.</summary>
    TranscriptRecognized,

    /// <summary>Recognizer produced nothing usable (silence / empty transcript).</summary>
    TranscriptEmpty,

    /// <summary>First token of the assistant reply arrived.</summary>
    ResponseStarted,

    /// <summary>Assistant reply finished and text-to-speech will read it aloud.</summary>
    ResponseCompletedWithSpeech,

    /// <summary>Assistant reply finished with no speech (TTS off or muted).</summary>
    ResponseCompletedSilently,

    /// <summary>Text-to-speech finished (or was cancelled) and the floor is free.</summary>
    SpeechCompleted,

    /// <summary>An error occurred (API failure, recognizer fault); return to Idle.</summary>
    Fault,
}
