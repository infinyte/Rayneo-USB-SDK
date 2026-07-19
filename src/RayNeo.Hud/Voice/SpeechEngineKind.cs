// -----------------------------------------------------------------------------
// SpeechEngineKind.cs
// Author: Kurt Mitchell
//
// Selects which ISpeechToText backend the voice stack builds at launch. Both
// engines sit behind the same interface, so the choice only affects wiring.
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>The speech-to-text backend to use for a HUD session.</summary>
public enum SpeechEngineKind
{
    /// <summary>Windows offline dictation via System.Speech (the default).</summary>
    System,

    /// <summary>Local Whisper recognition via Whisper.net.</summary>
    Whisper,
}
