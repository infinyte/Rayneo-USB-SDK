// -----------------------------------------------------------------------------
// ConversationHistory.cs
// Author: Kurt Mitchell
//
// Multi-turn conversation memory for a single session. Pure and UI-free so the
// turn-tracking and clear behaviour are unit-tested without audio or network.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// Ordered record of user/assistant turns for the current session. Alternation
/// is not enforced here (the loop appends a user turn on transcript and an
/// assistant turn when the reply completes), but empty turns are rejected so a
/// silent transcript or failed reply never enters the history sent to the model.
/// </summary>
public sealed class ConversationHistory
{
    private readonly List<ConversationTurn> _turns = new();

    /// <summary>
    /// A point-in-time snapshot of the turns so far, oldest first. Returns a copy
    /// so callers can enumerate it (e.g. to send to the assistant) without seeing
    /// concurrent appends.
    /// </summary>
    public IReadOnlyList<ConversationTurn> Turns => _turns.ToArray();

    /// <summary>Number of turns recorded.</summary>
    public int Count => _turns.Count;

    /// <summary>Raised whenever the history changes (turn added or cleared).</summary>
    public event EventHandler? Changed;

    /// <summary>Appends a user (spoken) turn.</summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null or whitespace.</exception>
    public void AddUserTurn(string text) => Add(ConversationRole.User, text);

    /// <summary>Appends an assistant (reply) turn.</summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null or whitespace.</exception>
    public void AddAssistantTurn(string text) => Add(ConversationRole.Assistant, text);

    /// <summary>Removes every turn, resetting the session memory.</summary>
    public void Clear()
    {
        if (_turns.Count == 0)
        {
            return;
        }
        _turns.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Add(ConversationRole role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A conversation turn must have non-empty text.", nameof(text));
        }
        _turns.Add(new ConversationTurn(role, text.Trim()));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
