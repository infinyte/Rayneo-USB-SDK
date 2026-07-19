// -----------------------------------------------------------------------------
// ISpeechToText.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// A speech recognizer driven by push-to-talk. <see cref="Start"/> begins mic
/// capture; while capturing, <see cref="PartialRecognized"/> fires with live
/// hypotheses; <see cref="Stop"/> ends capture and raises exactly one
/// <see cref="FinalRecognized"/> with the best final transcript (empty string if
/// nothing was recognized). Implementations must capture audio only between
/// Start and Stop and never persist it to disk (CLAUDE.md Phase 3). The
/// interface is engine-agnostic so a Whisper backend can replace the Windows one.
/// </summary>
public interface ISpeechToText : IDisposable
{
    /// <summary>Live partial transcript updated as the wearer speaks.</summary>
    event EventHandler<string> PartialRecognized;

    /// <summary>The final transcript, raised once after <see cref="Stop"/> (may be empty).</summary>
    event EventHandler<string> FinalRecognized;

    /// <summary>Raised if recognition fails; the loop treats this as a fault.</summary>
    event EventHandler<Exception> Failed;

    /// <summary>Begins microphone capture and recognition.</summary>
    void Start();

    /// <summary>Ends capture and emits the final transcript.</summary>
    void Stop();
}
