// -----------------------------------------------------------------------------
// AnthropicTurnTransport.cs
// Author: Kurt Mitchell
//
// IModelTurnTransport over the official Anthropic .NET SDK. This is the only
// type in RayNeo.Voice that touches the SDK's wire types: it maps the neutral
// ModelMessage conversation (including tool calls/results) onto Anthropic
// message params, declares IVoiceTool schemas as API tools, and folds the raw
// stream events back into the neutral ModelEvent shapes — accumulating partial
// tool-argument JSON until each call is complete.
//
// Auth: the SDK reads ANTHROPIC_API_KEY from the environment (CLAUDE.md
// Phase 2 — the key is never hardcoded, written, or logged).
// -----------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace Infinyte.RayNeo.Voice;

/// <summary>Streams model turns over the Anthropic Messages API.</summary>
public sealed class AnthropicTurnTransport : IModelTurnTransport
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly string _systemPrompt;
    private readonly int _maxTokens;

    /// <summary>Creates the transport.</summary>
    /// <param name="client">SDK client; reads ANTHROPIC_API_KEY from the environment.</param>
    /// <param name="model">Model name, e.g. <c>claude-opus-4-8</c>.</param>
    /// <param name="systemPrompt">System prompt for every turn.</param>
    /// <param name="maxTokens">Per-turn output token cap.</param>
    public AnthropicTurnTransport(AnthropicClient client, string model, string systemPrompt, int maxTokens)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = model;
        _systemPrompt = systemPrompt;
        _maxTokens = maxTokens;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ModelEvent> StreamTurnAsync(
        ModelTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = _maxTokens,
            System = _systemPrompt,
            Messages = BuildMessages(request.Messages),
            Tools = request.Tools.Count > 0 ? request.Tools.Select(BuildTool).ToList() : null,
        };

        // Tool-call arguments stream as partial JSON per content-block index;
        // each call is emitted as one complete ToolUseRequest at block stop.
        var pendingCalls = new Dictionary<long, (string Id, string Name, StringBuilder Json)>();
        ModelStopReason stopReason = ModelStopReason.EndTurn;

        await foreach (RawMessageStreamEvent streamEvent in
            _client.Messages.CreateStreaming(parameters).WithCancellation(cancellationToken))
        {
            if (streamEvent.TryPickContentBlockStart(out RawContentBlockStartEvent? blockStart))
            {
                if (blockStart.ContentBlock.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    pendingCalls[blockStart.Index] = (toolUse.ID, toolUse.Name, new StringBuilder());
                }
            }
            else if (streamEvent.TryPickContentBlockDelta(out RawContentBlockDeltaEvent? blockDelta))
            {
                if (blockDelta.Delta.TryPickText(out TextDelta? text) && !string.IsNullOrEmpty(text.Text))
                {
                    yield return new ModelEvent.TextDelta(text.Text);
                }
                else if (blockDelta.Delta.TryPickInputJson(out InputJsonDelta? inputJson) &&
                    pendingCalls.TryGetValue(blockDelta.Index, out var pending))
                {
                    pending.Json.Append(inputJson.PartialJson);
                }
            }
            else if (streamEvent.TryPickContentBlockStop(out RawContentBlockStopEvent? blockStop))
            {
                if (pendingCalls.Remove(blockStop.Index, out var completed))
                {
                    yield return new ModelEvent.ToolUseRequest(
                        completed.Id, completed.Name,
                        completed.Json.Length == 0 ? "{}" : completed.Json.ToString());
                }
            }
            else if (streamEvent.TryPickDelta(out RawMessageDeltaEvent? messageDelta) &&
                messageDelta.Delta.StopReason is not null)
            {
                stopReason = MapStopReason(messageDelta.Delta.StopReason.Raw());
            }
        }

        yield return new ModelEvent.TurnEnded(stopReason);
    }

    // ---- Neutral model → SDK params ----------------------------------------

    private static List<MessageParam> BuildMessages(IReadOnlyList<ModelMessage> messages)
    {
        var result = new List<MessageParam>(messages.Count);
        foreach (ModelMessage message in messages)
        {
            switch (message)
            {
                case ModelMessage.User user:
                    result.Add(new MessageParam { Role = Role.User, Content = user.Text });
                    break;

                case ModelMessage.Assistant assistant when assistant.ToolCalls.Count == 0:
                    result.Add(new MessageParam { Role = Role.Assistant, Content = assistant.Text });
                    break;

                case ModelMessage.Assistant assistant:
                {
                    var blocks = new List<ContentBlockParam>();
                    if (!string.IsNullOrEmpty(assistant.Text))
                    {
                        blocks.Add(new TextBlockParam(assistant.Text));
                    }
                    foreach (ModelToolCall call in assistant.ToolCalls)
                    {
                        blocks.Add(new ToolUseBlockParam
                        {
                            ID = call.Id,
                            Name = call.Name,
                            Input = ParseArgumentObject(call.ArgumentsJson),
                        });
                    }
                    result.Add(new MessageParam { Role = Role.Assistant, Content = blocks });
                    break;
                }

                case ModelMessage.ToolResults toolResults:
                {
                    var blocks = new List<ContentBlockParam>();
                    foreach (ModelToolResult toolResult in toolResults.Results)
                    {
                        blocks.Add(new ToolResultBlockParam(toolResult.ToolUseId)
                        {
                            Content = toolResult.Content,
                            IsError = toolResult.IsError,
                        });
                    }
                    result.Add(new MessageParam { Role = Role.User, Content = blocks });
                    break;
                }
            }
        }
        return result;
    }

    private static ToolUnion BuildTool(IVoiceTool tool)
    {
        var properties = new Dictionary<string, JsonElement>(tool.Parameters.Count);
        var required = new List<string>();
        foreach (VoiceToolParameter parameter in tool.Parameters)
        {
            properties[parameter.Name] = JsonSerializer.SerializeToElement(new
            {
                type = parameter.Type switch
                {
                    VoiceToolParameterType.Number => "number",
                    VoiceToolParameterType.Boolean => "boolean",
                    _ => "string",
                },
                description = parameter.Description,
            });
            if (parameter.IsRequired)
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new InputSchema
        {
            Properties = properties,
            Required = required.Count > 0 ? required : null,
        };
        return new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = schema,
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseArgumentObject(string argumentsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson)
                ?? new Dictionary<string, JsonElement>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static ModelStopReason MapStopReason(string raw) => raw switch
    {
        "end_turn" or "stop_sequence" => ModelStopReason.EndTurn,
        "tool_use" => ModelStopReason.ToolUse,
        "max_tokens" => ModelStopReason.MaxTokens,
        _ => ModelStopReason.Other,
    };
}
