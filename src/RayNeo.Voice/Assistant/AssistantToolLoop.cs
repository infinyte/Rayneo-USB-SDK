// -----------------------------------------------------------------------------
// AssistantToolLoop.cs
// Author: Kurt Mitchell
//
// The model↔tool orchestration loop. It streams model turns from an
// IModelTurnTransport, forwards text deltas to the caller, executes any tool
// calls the model makes, feeds the results back, and repeats until the model
// finishes (or the round limit stops a runaway loop). Pure orchestration — no
// SDK, audio, or UI — so every branch is unit-tested with a scripted transport.
// -----------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Infinyte.RayNeo.Voice;

/// <summary>Runs the agentic reply loop for one conversation turn.</summary>
public sealed class AssistantToolLoop
{
    private readonly IModelTurnTransport _transport;
    private readonly VoiceToolRegistry _registry;
    private readonly int _maxToolRounds;

    /// <summary>Creates the loop.</summary>
    /// <param name="maxToolRounds">
    /// Maximum number of tool-execution rounds per reply. When the model still
    /// requests tools after the last round, the loop stops with whatever text
    /// has streamed — a guard against runaway tool chains, not a normal path.
    /// </param>
    public AssistantToolLoop(IModelTurnTransport transport, VoiceToolRegistry registry, int maxToolRounds = 8)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _maxToolRounds = maxToolRounds;
    }

    /// <summary>Raised around each tool execution (started / succeeded / failed).</summary>
    public event EventHandler<ToolActivityEventArgs>? ToolActivity;

    /// <summary>
    /// Streams the assistant's reply to <paramref name="conversation"/> as text
    /// deltas, transparently executing tool calls between model turns.
    /// </summary>
    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ConversationTurn> conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<ModelMessage>(conversation.Count);
        foreach (ConversationTurn turn in conversation)
        {
            messages.Add(turn.Role == ConversationRole.User
                ? new ModelMessage.User(turn.Text)
                : new ModelMessage.Assistant(turn.Text, Array.Empty<ModelToolCall>()));
        }

        int round = 0;
        while (true)
        {
            var turnText = new StringBuilder();
            var calls = new List<ModelToolCall>();
            ModelStopReason stopReason = ModelStopReason.EndTurn;

            var request = new ModelTurnRequest(messages.ToArray(), _registry.Tools);
            await foreach (ModelEvent modelEvent in
                _transport.StreamTurnAsync(request, cancellationToken).WithCancellation(cancellationToken))
            {
                switch (modelEvent)
                {
                    case ModelEvent.TextDelta delta:
                        turnText.Append(delta.Text);
                        yield return delta.Text;
                        break;
                    case ModelEvent.ToolUseRequest toolUse:
                        calls.Add(new ModelToolCall(toolUse.Id, toolUse.Name, toolUse.ArgumentsJson));
                        break;
                    case ModelEvent.TurnEnded ended:
                        stopReason = ended.StopReason;
                        break;
                }
            }

            if (stopReason != ModelStopReason.ToolUse || calls.Count == 0 || round >= _maxToolRounds)
            {
                yield break;
            }
            round++;

            var results = new List<ModelToolResult>(calls.Count);
            foreach (ModelToolCall call in calls)
            {
                results.Add(await ExecuteToolAsync(call, cancellationToken).ConfigureAwait(false));
            }
            messages.Add(new ModelMessage.Assistant(turnText.ToString(), calls));
            messages.Add(new ModelMessage.ToolResults(results));
        }
    }

    // Executes one tool call. Failures become error results the model can react
    // to — never exceptions that would tear down the voice loop. Cancellation is
    // the one exception that propagates (barge-in must stop everything).
    private async Task<ModelToolResult> ExecuteToolAsync(ModelToolCall call, CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(call.Name, out IVoiceTool? tool))
        {
            return new ModelToolResult(call.Id, $"Unknown tool '{call.Name}'.", IsError: true);
        }

        ToolActivity?.Invoke(this, new ToolActivityEventArgs(call.Name, ToolActivityStatus.Started));
        try
        {
            JsonElement arguments = ParseArguments(call.ArgumentsJson);
            string content = await tool!.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
            ToolActivity?.Invoke(this, new ToolActivityEventArgs(call.Name, ToolActivityStatus.Succeeded));
            return new ModelToolResult(call.Id, content, IsError: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ToolActivity?.Invoke(this, new ToolActivityEventArgs(call.Name, ToolActivityStatus.Failed));
            return new ModelToolResult(call.Id, ex.Message, IsError: true);
        }
    }

    private static JsonElement ParseArguments(string argumentsJson)
    {
        using JsonDocument doc = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        return doc.RootElement.Clone();
    }
}
