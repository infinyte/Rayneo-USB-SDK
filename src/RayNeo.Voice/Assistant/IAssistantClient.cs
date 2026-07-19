// -----------------------------------------------------------------------------
// IAssistantClient.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// Streams an assistant reply for a conversation. The last turn in
/// <paramref name="conversation"/> is the newest user message; implementations
/// yield the reply as incremental text deltas so the HUD can render it as it
/// arrives. Kept as an interface so the Claude backend can be swapped (e.g. for
/// a local model) without touching the voice loop.
/// </summary>
public interface IAssistantClient
{
    /// <summary>
    /// Streams the reply to <paramref name="conversation"/> as text deltas.
    /// Cancelling <paramref name="cancellationToken"/> (e.g. on barge-in) ends
    /// the stream promptly.
    /// </summary>
    IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ConversationTurn> conversation,
        CancellationToken cancellationToken = default);
}
