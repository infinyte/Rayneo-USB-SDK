// -----------------------------------------------------------------------------
// VoiceInteractionControllerTests.cs
// Author: Kurt Mitchell
//
// The voice loop controller against fakes for push-to-talk, recognition,
// assistant, and synthesis: the full happy path, empty transcripts, mute,
// barge-in from every active state, faults, stray inputs, multi-turn history,
// and timer announcements. No audio, network, or UI (CLAUDE.md Phase 3).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class VoiceInteractionControllerTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // ---- Fakes --------------------------------------------------------------

    private sealed class FakePushToTalk : IPushToTalkSource
    {
        public int StartCalls { get; private set; }
        public event EventHandler? Pressed;
        public event EventHandler? Released;
        public void Start() => StartCalls++;
        public void Press() => Pressed?.Invoke(this, EventArgs.Empty);
        public void Release() => Released?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class FakeSpeechToText : ISpeechToText
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public event EventHandler<string>? PartialRecognized;
        public event EventHandler<string>? FinalRecognized;
        public event EventHandler<Exception>? Failed;
        public void Start() => StartCalls++;
        public void Stop() => StopCalls++;
        public void RaisePartial(string text) => PartialRecognized?.Invoke(this, text);
        public void RaiseFinal(string text) => FinalRecognized?.Invoke(this, text);
        public void RaiseFailed(Exception ex) => Failed?.Invoke(this, ex);
        public void Dispose() { }
    }

    private sealed class FakeTextToSpeech : ITextToSpeech
    {
        public List<string> Spoken { get; } = new();
        public int CancelCalls { get; private set; }
        public bool IsMuted { get; set; }
        public event EventHandler? SpeakCompleted;
        public void SpeakAsync(string text) => Spoken.Add(text);
        public void Cancel() => CancelCalls++;
        public void CompleteSpeech() => SpeakCompleted?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    /// <summary>
    /// Assistant whose reply is driven by the test through a channel, so tests
    /// control exactly when deltas arrive and when the stream completes.
    /// </summary>
    private sealed class FakeAssistant : IAssistantClient
    {
        private Channel<string> _channel = Channel.CreateUnbounded<string>();

        public List<int> ConversationLengths { get; } = new();
        public bool WasCancelled { get; private set; }
        public Exception? ThrowOnStream { get; set; }

        public void EmitDelta(string text) => _channel.Writer.TryWrite(text);
        public void CompleteReply() => _channel.Writer.TryComplete();

        public void ResetForNextTurn() => _channel = Channel.CreateUnbounded<string>();

        public async IAsyncEnumerable<string> StreamReplyAsync(
            IReadOnlyList<ConversationTurn> conversation,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ConversationLengths.Add(conversation.Count);
            if (ThrowOnStream is not null)
            {
                throw ThrowOnStream;
            }
            ChannelReader<string> reader = _channel.Reader;
            while (true)
            {
                string delta;
                try
                {
                    if (!await reader.WaitToReadAsync(cancellationToken))
                    {
                        yield break;
                    }
                    if (!reader.TryRead(out delta!))
                    {
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    WasCancelled = true;
                    throw;
                }
                yield return delta;
            }
        }
    }

    // ---- Harness ------------------------------------------------------------

    private readonly FakePushToTalk _ptt = new();
    private readonly FakeSpeechToText _stt = new();
    private readonly FakeTextToSpeech _tts = new();
    private readonly FakeAssistant _assistant = new();
    private readonly ConversationHistory _history = new();
    private readonly TimerService _timers;
    private readonly FakeTimeProvider _clock = new();
    private readonly VoiceInteractionController _controller;
    private readonly List<VoiceState> _states = new();

    public VoiceInteractionControllerTests()
    {
        _timers = new TimerService(_clock);
        _controller = new VoiceInteractionController(_ptt, _stt, _assistant, _tts, _history, _timers);
        _controller.StateChanged += (_, e) => { lock (_states) { _states.Add(e.NewState); } };
        _controller.Start();
    }

    public void Dispose() => _controller.Dispose();

    /// <summary>Waits until the controller reaches <paramref name="state"/>.</summary>
    private async Task WaitForState(VoiceState state)
    {
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (_controller.CurrentState != state)
        {
            Assert.True(DateTime.UtcNow < deadline,
                $"Timed out waiting for {state}; current state is {_controller.CurrentState}.");
            await Task.Delay(10);
        }
    }

    /// <summary>Drives press → release → final transcript, landing in Thinking.</summary>
    private async Task SpeakUtterance(string text)
    {
        _ptt.Press();
        _ptt.Release();
        _stt.RaiseFinal(text);
        await WaitForState(VoiceState.Thinking);
    }

    // ---- Startup ------------------------------------------------------------

    [Fact]
    public void StartsIdle_AndStartsThePushToTalkSource()
    {
        Assert.Equal(VoiceState.Idle, _controller.CurrentState);
        Assert.Equal(1, _ptt.StartCalls);
    }

    // ---- Happy path ---------------------------------------------------------

    [Fact]
    public async Task HappyPath_RunsTheFullCycle()
    {
        var deltas = new List<string>();
        _controller.ReplyDelta += (_, d) => deltas.Add(d);

        _ptt.Press();
        Assert.Equal(VoiceState.Listening, _controller.CurrentState);
        Assert.Equal(1, _stt.StartCalls);

        _ptt.Release();
        Assert.Equal(VoiceState.Transcribing, _controller.CurrentState);
        Assert.Equal(1, _stt.StopCalls);

        _stt.RaiseFinal("what time is it");
        await WaitForState(VoiceState.Thinking);
        Assert.Equal("what time is it", _history.Turns[0].Text);

        _assistant.EmitDelta("It is ");
        await WaitForState(VoiceState.Streaming);
        _assistant.EmitDelta("noon.");
        _assistant.CompleteReply();
        await WaitForState(VoiceState.Speaking);

        Assert.Equal(new[] { "It is ", "noon." }, deltas.ToArray());
        Assert.Equal("It is noon.", _history.Turns[1].Text);
        Assert.Equal("It is noon.", Assert.Single(_tts.Spoken));

        _tts.CompleteSpeech();
        Assert.Equal(VoiceState.Idle, _controller.CurrentState);
    }

    [Fact]
    public async Task SecondTurn_SendsGrownHistoryToTheAssistant()
    {
        await SpeakUtterance("first");
        _assistant.EmitDelta("One.");
        _assistant.CompleteReply();
        await WaitForState(VoiceState.Speaking);
        _tts.CompleteSpeech();

        _assistant.ResetForNextTurn();
        await SpeakUtterance("second");
        _assistant.CompleteReply();
        await WaitForState(VoiceState.Idle);

        Assert.Equal(new[] { 1, 3 }, _assistant.ConversationLengths.ToArray());
    }

    [Fact]
    public void PartialTranscripts_AreForwardedWhileListening()
    {
        var partials = new List<string>();
        _controller.PartialTranscript += (_, p) => partials.Add(p);

        _ptt.Press();
        _stt.RaisePartial("what");
        _stt.RaisePartial("what time");

        Assert.Equal(new[] { "what", "what time" }, partials.ToArray());
    }

    // ---- Empty transcript / empty reply / mute ------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyTranscript_ReturnsToIdle_WithoutCallingAssistant(string transcript)
    {
        _ptt.Press();
        _ptt.Release();
        _stt.RaiseFinal(transcript);
        await WaitForState(VoiceState.Idle);

        Assert.Empty(_assistant.ConversationLengths);
        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public async Task MutedReply_SkipsSpeaking_ButKeepsHistory()
    {
        _tts.IsMuted = true;
        await SpeakUtterance("hello");
        _assistant.EmitDelta("Hi there.");
        _assistant.CompleteReply();
        await WaitForState(VoiceState.Idle);

        Assert.Empty(_tts.Spoken);
        Assert.Equal("Hi there.", _history.Turns[1].Text);
    }

    [Fact]
    public async Task EmptyReply_ReturnsToIdle_WithNoAssistantTurnOrSpeech()
    {
        await SpeakUtterance("hello");
        _assistant.CompleteReply(); // stream ends with zero deltas
        await WaitForState(VoiceState.Idle);

        Assert.Empty(_tts.Spoken);
        Assert.Equal(1, _history.Count); // just the user turn
    }

    // ---- Barge-in -----------------------------------------------------------

    [Fact]
    public async Task BargeIn_DuringThinking_CancelsTheRequest()
    {
        await SpeakUtterance("hello");

        _ptt.Press();
        Assert.Equal(VoiceState.Listening, _controller.CurrentState);
        await _controller.WaitForReplyTaskAsync();

        Assert.True(_assistant.WasCancelled);
        Assert.Equal(2, _stt.StartCalls);
    }

    [Fact]
    public async Task BargeIn_DuringStreaming_CancelsAndDiscardsThePartialReply()
    {
        await SpeakUtterance("hello");
        _assistant.EmitDelta("Well, ");
        await WaitForState(VoiceState.Streaming);

        _ptt.Press();
        Assert.Equal(VoiceState.Listening, _controller.CurrentState);
        await _controller.WaitForReplyTaskAsync();

        Assert.True(_assistant.WasCancelled);
        Assert.Equal(1, _history.Count); // partial reply never entered history
        Assert.Empty(_tts.Spoken);
    }

    [Fact]
    public async Task BargeIn_DuringSpeaking_CancelsPlayback()
    {
        await SpeakUtterance("hello");
        _assistant.EmitDelta("Hi.");
        _assistant.CompleteReply();
        await WaitForState(VoiceState.Speaking);

        _ptt.Press();

        Assert.Equal(VoiceState.Listening, _controller.CurrentState);
        Assert.Equal(1, _tts.CancelCalls);
    }

    // ---- Faults -------------------------------------------------------------

    [Fact]
    public async Task AssistantFailure_FaultsToIdle_AndReportsTheError()
    {
        var errors = new List<string>();
        var faulted = new TaskCompletionSource();
        _controller.ErrorOccurred += (_, e) => { errors.Add(e); faulted.TrySetResult(); };
        _assistant.ThrowOnStream = new InvalidOperationException("API unreachable");

        // The fault can fire before Thinking is even observable, so drive the
        // loop directly and wait on the error rather than the transient state.
        _ptt.Press();
        _ptt.Release();
        _stt.RaiseFinal("hello");
        await faulted.Task.WaitAsync(Timeout);
        await WaitForState(VoiceState.Idle);

        Assert.Contains(errors, e => e.Contains("API unreachable"));
        Assert.Equal(1, _history.Count); // no assistant turn
    }

    [Fact]
    public void RecognizerFailure_WhileListening_FaultsToIdle()
    {
        var errors = new List<string>();
        _controller.ErrorOccurred += (_, e) => errors.Add(e);

        _ptt.Press();
        _stt.RaiseFailed(new InvalidOperationException("mic lost"));

        Assert.Equal(VoiceState.Idle, _controller.CurrentState);
        Assert.Contains(errors, e => e.Contains("mic lost"));
    }

    // ---- Stray inputs -------------------------------------------------------

    [Fact]
    public void StrayRelease_WhileIdle_IsIgnored()
    {
        _ptt.Release();
        Assert.Equal(VoiceState.Idle, _controller.CurrentState);
        Assert.Equal(0, _stt.StopCalls);
    }

    [Fact]
    public void RepeatedPress_WhileListening_IsIgnored()
    {
        _ptt.Press();
        _ptt.Press();
        Assert.Equal(VoiceState.Listening, _controller.CurrentState);
        Assert.Equal(1, _stt.StartCalls);
    }

    [Fact]
    public async Task StaleFinalTranscript_AfterReturningToIdle_IsIgnored()
    {
        _ptt.Press();
        _ptt.Release();
        _stt.RaiseFinal("");
        await WaitForState(VoiceState.Idle);

        _stt.RaiseFinal("late arrival"); // recognizer echo after the loop reset
        Assert.Equal(VoiceState.Idle, _controller.CurrentState);
        Assert.Empty(_assistant.ConversationLengths);
    }

    // ---- Timers -------------------------------------------------------------

    [Fact]
    public void ExpiredTimer_WhileIdle_IsAnnouncedOnHudAndSpoken()
    {
        var announcements = new List<string>();
        _controller.TimerAnnouncement += (_, a) => announcements.Add(a);

        _timers.StartTimer("tea", TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Contains(announcements, a => a.Contains("tea"));
        Assert.Contains(_tts.Spoken, s => s.Contains("tea"));
        Assert.Equal(VoiceState.Idle, _controller.CurrentState);
    }

    [Fact]
    public void ExpiredTimer_WhileListening_ShowsOnHudButDoesNotSpeakOverTheMic()
    {
        var announcements = new List<string>();
        _controller.TimerAnnouncement += (_, a) => announcements.Add(a);

        _timers.StartTimer("tea", TimeSpan.FromMinutes(1));
        _ptt.Press();
        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Contains(announcements, a => a.Contains("tea"));
        Assert.Empty(_tts.Spoken);
    }
}
