// -----------------------------------------------------------------------------
// SessionTools.cs
// Author: Kurt Mitchell
//
// Session-level tools: current time, TTS mute, and conversation reset. Each is
// a thin DelegateVoiceTool over an injected dependency so behaviour is
// unit-testable with fakes.
// -----------------------------------------------------------------------------

using System.Globalization;

namespace Infinyte.RayNeo.Voice;

/// <summary>Factory for the session tool set.</summary>
public static class SessionTools
{
    /// <summary>Creates the <c>get_current_time</c> tool.</summary>
    public static IVoiceTool CreateGetCurrentTime(TimeProvider timeProvider) => new DelegateVoiceTool(
        "get_current_time",
        "Get the current local date and time.",
        Array.Empty<VoiceToolParameter>(),
        (_, _) =>
        {
            DateTimeOffset now = timeProvider.GetLocalNow();
            return Task.FromResult(string.Create(CultureInfo.InvariantCulture,
                $"It is {now:dddd, MMMM d, yyyy} at {now:H:mm} ({now:h:mm tt})."));
        });

    /// <summary>Creates the <c>set_speech_muted</c> tool controlling <paramref name="textToSpeech"/>.</summary>
    public static IVoiceTool CreateSetSpeechMuted(ITextToSpeech textToSpeech) => new DelegateVoiceTool(
        "set_speech_muted",
        "Mute or unmute the assistant's spoken replies. Replies stay visible on the glasses.",
        new[]
        {
            new VoiceToolParameter("muted", "True to mute speech, false to unmute.",
                VoiceToolParameterType.Boolean, IsRequired: true),
        },
        (args, _) =>
        {
            bool muted = args.GetRequiredBoolean("muted");
            textToSpeech.IsMuted = muted;
            return Task.FromResult(muted ? "Speech is now muted." : "Speech is now unmuted.");
        });

    /// <summary>Creates the <c>clear_conversation</c> tool over <paramref name="history"/>.</summary>
    public static IVoiceTool CreateClearConversation(ConversationHistory history) => new DelegateVoiceTool(
        "clear_conversation",
        "Forget the current conversation and start fresh.",
        Array.Empty<VoiceToolParameter>(),
        (_, _) =>
        {
            history.Clear();
            return Task.FromResult("Conversation history cleared.");
        });
}
