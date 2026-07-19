// -----------------------------------------------------------------------------
// ClaudeAssistantClient.cs
// Author: Kurt Mitchell
//
// IAssistantClient backed by Anthropic's Claude API (official Anthropic .NET
// SDK). Streams the reply as text deltas so the HUD can render it token-by-token.
//
// Model: claude-opus-4-8 by default (configurable). Thinking is left off for a
// low-latency conversational turn — a hands-free HUD wants the first words on
// the glass fast, not a deliberation pause. The system prompt asks for short,
// plain-text answers because the reply is both read on a ~40° FOV display and
// spoken aloud by the synthesizer.
//
// Auth: the SDK reads ANTHROPIC_API_KEY from the environment. If it is missing
// or the call fails, StreamReplyAsync throws; the voice loop catches it and
// shows a brief on-glass error rather than crashing (CLAUDE.md Phase 3).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Anthropic;
using Anthropic.Models.Messages;

namespace Infinyte.RayNeo.Voice;

/// <summary>Streams Claude replies for the voice loop over the Anthropic API.</summary>
public sealed class ClaudeAssistantClient : IAssistantClient
{
    private const string DefaultModel = "claude-opus-4-8";

    private const string DefaultSystemPrompt =
        "You are a voice assistant embedded in AR glasses. Your reply is shown on a " +
        "small heads-up display and read aloud, so answer in one to three short, " +
        "spoken-style sentences. Use plain text only — no markdown, lists, headings, " +
        "or code blocks. Lead with the answer; skip preamble.";

    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly string _systemPrompt;
    private readonly int _maxTokens;

    /// <summary>
    /// Creates a client. <paramref name="client"/> is injectable for testing;
    /// when null a default <see cref="AnthropicClient"/> is used, which reads the
    /// API key from the ANTHROPIC_API_KEY environment variable.
    /// </summary>
    public ClaudeAssistantClient(
        AnthropicClient? client = null,
        string? model = null,
        string? systemPrompt = null,
        int maxTokens = 1024)
    {
        _client = client ?? new AnthropicClient();
        _model = model ?? DefaultModel;
        _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
        _maxTokens = maxTokens;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ConversationTurn> conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = _maxTokens,
            System = _systemPrompt,
            Messages = BuildMessages(conversation),
        };

        await foreach (RawMessageStreamEvent streamEvent in
            _client.Messages.CreateStreaming(parameters).WithCancellation(cancellationToken))
        {
            if (streamEvent.TryPickContentBlockDelta(out RawContentBlockDeltaEvent? delta) &&
                delta.Delta.TryPickText(out TextDelta? text) &&
                !string.IsNullOrEmpty(text.Text))
            {
                yield return text.Text;
            }
        }
    }

    private static List<MessageParam> BuildMessages(IReadOnlyList<ConversationTurn> conversation)
    {
        var messages = new List<MessageParam>(conversation.Count);
        foreach (ConversationTurn turn in conversation)
        {
            messages.Add(new MessageParam
            {
                Role = turn.Role == ConversationRole.User ? Role.User : Role.Assistant,
                Content = turn.Text,
            });
        }
        return messages;
    }
}
