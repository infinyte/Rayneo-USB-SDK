// -----------------------------------------------------------------------------
// ModelMessage.cs
// Author: Kurt Mitchell
//
// The transport-neutral conversation model sent to IModelTurnTransport. It
// mirrors the tool-use message shapes every major model API shares (user text,
// assistant text plus tool calls, tool results) without depending on any SDK.
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>A tool call the assistant made in a previous model turn.</summary>
/// <param name="Id">Provider-assigned call id.</param>
/// <param name="Name">The tool's name.</param>
/// <param name="ArgumentsJson">The call's argument object as JSON text.</param>
public sealed record ModelToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>The outcome of executing one tool call.</summary>
/// <param name="ToolUseId">The call id this result answers.</param>
/// <param name="Content">Result text the model reads.</param>
/// <param name="IsError">True when the tool failed or was unknown.</param>
public sealed record ModelToolResult(string ToolUseId, string Content, bool IsError);

/// <summary>One message in the conversation sent to the model.</summary>
public abstract record ModelMessage
{
    private ModelMessage() { }

    /// <summary>A user (wearer) message.</summary>
    /// <param name="Text">The spoken, transcribed text.</param>
    public sealed record User(string Text) : ModelMessage;

    /// <summary>An assistant turn: reply text and any tool calls it made.</summary>
    /// <param name="Text">The turn's text (may be empty when it only called tools).</param>
    /// <param name="ToolCalls">Tool calls made in the turn (may be empty).</param>
    public sealed record Assistant(string Text, IReadOnlyList<ModelToolCall> ToolCalls) : ModelMessage;

    /// <summary>Tool results answering the assistant's previous tool calls.</summary>
    /// <param name="Results">One result per call, in call order.</param>
    public sealed record ToolResults(IReadOnlyList<ModelToolResult> Results) : ModelMessage;
}
