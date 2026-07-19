// -----------------------------------------------------------------------------
// AssistantToolLoopTests.cs
// Author: Kurt Mitchell
//
// The model↔tool orchestration loop, exercised against a scripted fake
// transport: text-only turns, single and chained tool calls, unknown tools,
// tool failures, the round limit, cancellation, and the exact messages the
// loop sends back to the model after executing tools.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class AssistantToolLoopTests
{
    // ---- Test doubles -------------------------------------------------------

    /// <summary>Scripted transport: returns one pre-baked event list per request.</summary>
    private sealed class FakeTransport : IModelTurnTransport
    {
        private readonly Queue<IReadOnlyList<ModelEvent>> _turns = new();

        public List<ModelTurnRequest> Requests { get; } = new();

        public void EnqueueTurn(params ModelEvent[] events) => _turns.Enqueue(events);

        public async IAsyncEnumerable<ModelEvent> StreamTurnAsync(
            ModelTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_turns.Count == 0)
            {
                throw new InvalidOperationException("FakeTransport ran out of scripted turns.");
            }
            foreach (ModelEvent e in _turns.Dequeue())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return e;
                await Task.Yield();
            }
        }
    }

    private static DelegateVoiceTool Tool(string name, Func<VoiceToolArguments, string> body) => new(
        name, $"Test tool {name}.",
        Array.Empty<VoiceToolParameter>(),
        (args, _) => Task.FromResult(body(args)));

    private static IReadOnlyList<ConversationTurn> Ask(string text) =>
        new[] { new ConversationTurn(ConversationRole.User, text) };

    private static async Task<string> Collect(IAsyncEnumerable<string> stream)
    {
        var parts = new List<string>();
        await foreach (string s in stream)
        {
            parts.Add(s);
        }
        return string.Concat(parts);
    }

    // ---- Text-only turns ----------------------------------------------------

    [Fact]
    public async Task TextOnlyTurn_StreamsDeltas_AndMakesOneRequest()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.TextDelta("Hello "),
            new ModelEvent.TextDelta("world."),
            new ModelEvent.TurnEnded(ModelStopReason.EndTurn));
        var loop = new AssistantToolLoop(transport, new VoiceToolRegistry());

        string reply = await Collect(loop.StreamReplyAsync(Ask("hi")));

        Assert.Equal("Hello world.", reply);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ConversationTurns_MapToModelMessages_InOrder()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(new ModelEvent.TurnEnded(ModelStopReason.EndTurn));
        var loop = new AssistantToolLoop(transport, new VoiceToolRegistry());

        var conversation = new[]
        {
            new ConversationTurn(ConversationRole.User, "one"),
            new ConversationTurn(ConversationRole.Assistant, "two"),
            new ConversationTurn(ConversationRole.User, "three"),
        };
        await Collect(loop.StreamReplyAsync(conversation));

        IReadOnlyList<ModelMessage> messages = transport.Requests[0].Messages;
        Assert.Equal(3, messages.Count);
        Assert.Equal("one", Assert.IsType<ModelMessage.User>(messages[0]).Text);
        Assert.Equal("two", Assert.IsType<ModelMessage.Assistant>(messages[1]).Text);
        Assert.Equal("three", Assert.IsType<ModelMessage.User>(messages[2]).Text);
    }

    [Fact]
    public async Task RegistryTools_AreOfferedToTheModel()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(new ModelEvent.TurnEnded(ModelStopReason.EndTurn));
        var registry = new VoiceToolRegistry();
        registry.Register(Tool("get_time", _ => "12:00"));
        var loop = new AssistantToolLoop(transport, registry);

        await Collect(loop.StreamReplyAsync(Ask("hi")));

        Assert.Equal(new[] { "get_time" }, transport.Requests[0].Tools.Select(t => t.Name).ToArray());
    }

    // ---- Tool execution -----------------------------------------------------

    [Fact]
    public async Task ToolUseTurn_ExecutesTool_AndContinuesWithResult()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.TextDelta("Checking. "),
            new ModelEvent.ToolUseRequest("call_1", "get_time", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(
            new ModelEvent.TextDelta("It is noon."),
            new ModelEvent.TurnEnded(ModelStopReason.EndTurn));

        var registry = new VoiceToolRegistry();
        registry.Register(Tool("get_time", _ => "12:00 noon"));
        var loop = new AssistantToolLoop(transport, registry);

        string reply = await Collect(loop.StreamReplyAsync(Ask("what time is it")));

        Assert.Equal("Checking. It is noon.", reply);
        Assert.Equal(2, transport.Requests.Count);

        // The follow-up request must carry the assistant turn (text + call) and the result.
        IReadOnlyList<ModelMessage> messages = transport.Requests[1].Messages;
        var assistant = Assert.IsType<ModelMessage.Assistant>(messages[^2]);
        Assert.Equal("Checking. ", assistant.Text);
        ModelToolCall call = Assert.Single(assistant.ToolCalls);
        Assert.Equal(("call_1", "get_time", "{}"), (call.Id, call.Name, call.ArgumentsJson));

        var results = Assert.IsType<ModelMessage.ToolResults>(messages[^1]);
        ModelToolResult result = Assert.Single(results.Results);
        Assert.Equal("call_1", result.ToolUseId);
        Assert.Equal("12:00 noon", result.Content);
        Assert.False(result.IsError);
    }

    [Fact]
    public async Task ToolArguments_AreParsedAndPassedThrough()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c1", "echo", "{\"text\":\"marco\"}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(new ModelEvent.TurnEnded(ModelStopReason.EndTurn));

        var registry = new VoiceToolRegistry();
        registry.Register(Tool("echo", args => "polo: " + args.GetRequiredString("text")));
        var loop = new AssistantToolLoop(transport, registry);

        await Collect(loop.StreamReplyAsync(Ask("hi")));

        var results = Assert.IsType<ModelMessage.ToolResults>(transport.Requests[1].Messages[^1]);
        Assert.Equal("polo: marco", results.Results[0].Content);
    }

    [Fact]
    public async Task ChainedToolCalls_RunAcrossMultipleRounds()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c1", "step", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c2", "step", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(
            new ModelEvent.TextDelta("Done."),
            new ModelEvent.TurnEnded(ModelStopReason.EndTurn));

        int calls = 0;
        var registry = new VoiceToolRegistry();
        registry.Register(Tool("step", _ => $"step {++calls}"));
        var loop = new AssistantToolLoop(transport, registry);

        string reply = await Collect(loop.StreamReplyAsync(Ask("go")));

        Assert.Equal("Done.", reply);
        Assert.Equal(2, calls);
        Assert.Equal(3, transport.Requests.Count);
    }

    [Fact]
    public async Task UnknownTool_ReturnsErrorResult_AndLoopContinues()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c1", "not_registered", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(
            new ModelEvent.TextDelta("Sorry."),
            new ModelEvent.TurnEnded(ModelStopReason.EndTurn));
        var loop = new AssistantToolLoop(transport, new VoiceToolRegistry());

        string reply = await Collect(loop.StreamReplyAsync(Ask("hi")));

        Assert.Equal("Sorry.", reply);
        var results = Assert.IsType<ModelMessage.ToolResults>(transport.Requests[1].Messages[^1]);
        Assert.True(results.Results[0].IsError);
        Assert.Contains("not_registered", results.Results[0].Content);
    }

    [Fact]
    public async Task ToolThrow_BecomesErrorResult_NotACrash()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c1", "boom", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(
            new ModelEvent.TextDelta("That failed."),
            new ModelEvent.TurnEnded(ModelStopReason.EndTurn));

        var registry = new VoiceToolRegistry();
        registry.Register(Tool("boom", _ => throw new InvalidOperationException("kettle offline")));
        var loop = new AssistantToolLoop(transport, registry);

        string reply = await Collect(loop.StreamReplyAsync(Ask("hi")));

        Assert.Equal("That failed.", reply);
        var results = Assert.IsType<ModelMessage.ToolResults>(transport.Requests[1].Messages[^1]);
        Assert.True(results.Results[0].IsError);
        Assert.Contains("kettle offline", results.Results[0].Content);
    }

    [Fact]
    public async Task RoundLimit_StopsRunawayToolLoops()
    {
        var transport = new FakeTransport();
        for (int i = 0; i < 10; i++)
        {
            transport.EnqueueTurn(
                new ModelEvent.ToolUseRequest($"c{i}", "step", "{}"),
                new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        }

        var registry = new VoiceToolRegistry();
        registry.Register(Tool("step", _ => "ok"));
        var loop = new AssistantToolLoop(transport, registry, maxToolRounds: 2);

        await Collect(loop.StreamReplyAsync(Ask("go")));

        // Initial request + 2 tool rounds; the third tool request is not honoured.
        Assert.Equal(3, transport.Requests.Count);
    }

    // ---- Activity events ----------------------------------------------------

    [Fact]
    public async Task ToolActivity_ReportsStartAndSuccess()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c1", "get_time", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(new ModelEvent.TurnEnded(ModelStopReason.EndTurn));

        var registry = new VoiceToolRegistry();
        registry.Register(Tool("get_time", _ => "12:00"));
        var loop = new AssistantToolLoop(transport, registry);

        var activity = new List<(string Name, ToolActivityStatus Status)>();
        loop.ToolActivity += (_, e) => activity.Add((e.ToolName, e.Status));

        await Collect(loop.StreamReplyAsync(Ask("hi")));

        Assert.Equal(
            new[] { ("get_time", ToolActivityStatus.Started), ("get_time", ToolActivityStatus.Succeeded) },
            activity.ToArray());
    }

    [Fact]
    public async Task ToolActivity_ReportsFailure()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            new ModelEvent.ToolUseRequest("c1", "boom", "{}"),
            new ModelEvent.TurnEnded(ModelStopReason.ToolUse));
        transport.EnqueueTurn(new ModelEvent.TurnEnded(ModelStopReason.EndTurn));

        var registry = new VoiceToolRegistry();
        registry.Register(Tool("boom", _ => throw new InvalidOperationException("nope")));
        var loop = new AssistantToolLoop(transport, registry);

        var statuses = new List<ToolActivityStatus>();
        loop.ToolActivity += (_, e) => statuses.Add(e.Status);

        await Collect(loop.StreamReplyAsync(Ask("hi")));

        Assert.Equal(new[] { ToolActivityStatus.Started, ToolActivityStatus.Failed }, statuses.ToArray());
    }

    // ---- Cancellation -------------------------------------------------------

    [Fact]
    public async Task Cancellation_StopsTheStreamPromptly()
    {
        var transport = new FakeTransport();
        transport.EnqueueTurn(
            Enumerable.Repeat(new ModelEvent.TextDelta("x") as ModelEvent, 1000)
                .Append(new ModelEvent.TurnEnded(ModelStopReason.EndTurn))
                .ToArray());
        var loop = new AssistantToolLoop(transport, new VoiceToolRegistry());

        using var cts = new CancellationTokenSource();
        int received = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (string _ in loop.StreamReplyAsync(Ask("hi"), cts.Token))
            {
                if (++received == 3)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.True(received < 1000, "stream should stop shortly after cancellation");
    }
}
