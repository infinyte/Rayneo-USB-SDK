// -----------------------------------------------------------------------------
// ConversationTurn.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>Author of a conversation turn.</summary>
public enum ConversationRole
{
    /// <summary>Spoken by the wearer (transcribed).</summary>
    User,

    /// <summary>Produced by the assistant.</summary>
    Assistant,
}

/// <summary>A single immutable turn in the conversation.</summary>
/// <param name="Role">Who authored the turn.</param>
/// <param name="Text">The turn's text content.</param>
public readonly record struct ConversationTurn(ConversationRole Role, string Text);
