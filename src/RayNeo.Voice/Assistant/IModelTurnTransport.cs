// -----------------------------------------------------------------------------
// IModelTurnTransport.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>Everything one model turn needs: the conversation and the tools on offer.</summary>
/// <param name="Messages">The conversation so far, oldest first.</param>
/// <param name="Tools">Tools the model may call (may be empty).</param>
public sealed record ModelTurnRequest(
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<IVoiceTool> Tools);

/// <summary>
/// Streams a single model turn as transport-neutral <see cref="ModelEvent"/>s.
/// <see cref="AnthropicTurnTransport"/> is the production implementation; tests
/// script a fake. The stream always ends with <see cref="ModelEvent.TurnEnded"/>
/// (unless cancelled or faulted), and cancelling the token ends it promptly.
/// </summary>
public interface IModelTurnTransport
{
    /// <summary>Streams the events of one model turn for <paramref name="request"/>.</summary>
    IAsyncEnumerable<ModelEvent> StreamTurnAsync(
        ModelTurnRequest request,
        CancellationToken cancellationToken = default);
}
