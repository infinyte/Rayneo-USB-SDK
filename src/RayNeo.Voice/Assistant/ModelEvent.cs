// -----------------------------------------------------------------------------
// ModelEvent.cs
// Author: Kurt Mitchell
//
// The transport-neutral streaming event model. IModelTurnTransport translates
// a provider's wire events into these three shapes, so AssistantToolLoop (and
// its tests) never touch a model SDK.
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>Why a model turn stopped.</summary>
public enum ModelStopReason
{
    /// <summary>The model finished its reply.</summary>
    EndTurn,

    /// <summary>The model paused to have tools executed.</summary>
    ToolUse,

    /// <summary>The reply hit the token limit.</summary>
    MaxTokens,

    /// <summary>Any other stop reason.</summary>
    Other,
}

/// <summary>One event in a streamed model turn.</summary>
public abstract record ModelEvent
{
    private ModelEvent() { }

    /// <summary>A chunk of reply text.</summary>
    /// <param name="Text">The text delta (never empty).</param>
    public sealed record TextDelta(string Text) : ModelEvent;

    /// <summary>
    /// A complete tool call requested by the model. The transport accumulates
    /// partial argument JSON internally and emits this once the call is whole.
    /// </summary>
    /// <param name="Id">Provider-assigned call id, echoed back in the result.</param>
    /// <param name="Name">The tool's name.</param>
    /// <param name="ArgumentsJson">The call's argument object as JSON text.</param>
    public sealed record ToolUseRequest(string Id, string Name, string ArgumentsJson) : ModelEvent;

    /// <summary>The turn finished; always the final event of a stream.</summary>
    /// <param name="StopReason">Why the model stopped.</param>
    public sealed record TurnEnded(ModelStopReason StopReason) : ModelEvent;
}
