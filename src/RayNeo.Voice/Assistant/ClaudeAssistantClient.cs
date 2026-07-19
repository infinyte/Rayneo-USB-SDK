// -----------------------------------------------------------------------------
// ClaudeAssistantClient.cs
// Author: Kurt Mitchell
//
// IAssistantClient backed by Anthropic's Claude API (official Anthropic .NET
// SDK). Composes AnthropicTurnTransport (the only SDK-touching type) with
// AssistantToolLoop, so replies stream as text deltas while tool calls the
// model makes are executed transparently between turns.
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

using Anthropic;

namespace Infinyte.RayNeo.Voice;

/// <summary>Streams Claude replies (with tool use) for the voice loop.</summary>
public sealed class ClaudeAssistantClient : IAssistantClient
{
    private const string DefaultModel = "claude-opus-4-8";

    private const string DefaultSystemPrompt =
        "You are a voice assistant embedded in AR glasses. Your reply is shown on a " +
        "small heads-up display and read aloud, so answer in one to three short, " +
        "spoken-style sentences. Use plain text only — no markdown, lists, headings, " +
        "or code blocks. Lead with the answer; skip preamble.";

    private const string ToolGuidance =
        " You have tools; use them whenever they help (timers, notes, apps, session " +
        "control) and then confirm what you did in a few words. Never invent a tool " +
        "outcome — report what the tool actually returned.";

    private readonly AssistantToolLoop _loop;

    /// <summary>
    /// Creates a client. <paramref name="client"/> is injectable for testing;
    /// when null a default <see cref="AnthropicClient"/> is used, which reads the
    /// API key from the ANTHROPIC_API_KEY environment variable.
    /// <paramref name="tools"/> is the tool set offered to the model; when null
    /// or empty the client behaves as a plain conversational assistant.
    /// </summary>
    public ClaudeAssistantClient(
        AnthropicClient? client = null,
        string? model = null,
        string? systemPrompt = null,
        int maxTokens = 1024,
        VoiceToolRegistry? tools = null)
    {
        tools ??= new VoiceToolRegistry();

        string prompt = systemPrompt ?? (tools.Tools.Count > 0
            ? DefaultSystemPrompt + ToolGuidance
            : DefaultSystemPrompt);

        var transport = new AnthropicTurnTransport(
            client ?? new AnthropicClient(), model ?? DefaultModel, prompt, maxTokens);
        _loop = new AssistantToolLoop(transport, tools);
        _loop.ToolActivity += (_, e) => ToolActivity?.Invoke(this, e);
    }

    /// <summary>Raised around each tool execution so the HUD can show activity.</summary>
    public event EventHandler<ToolActivityEventArgs>? ToolActivity;

    /// <inheritdoc/>
    public IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ConversationTurn> conversation,
        CancellationToken cancellationToken = default) =>
        _loop.StreamReplyAsync(conversation, cancellationToken);
}
