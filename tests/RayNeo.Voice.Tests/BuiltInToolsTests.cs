// -----------------------------------------------------------------------------
// BuiltInToolsTests.cs
// Author: Kurt Mitchell
//
// The platform-neutral built-in tools: timers (start/cancel/list), current
// time, TTS mute, and conversation clearing. Each executes against fakes; the
// returned strings are what the model reads back, so their content is asserted.
// -----------------------------------------------------------------------------

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infinyte.RayNeo.Voice;

namespace RayNeo.Voice.Tests;

public sealed class BuiltInToolsTests
{
    private static JsonElement Args(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class FakeTextToSpeech : ITextToSpeech
    {
        public bool IsMuted { get; set; }
        public event EventHandler? SpeakCompleted { add { } remove { } }
        public void SpeakAsync(string text) { }
        public void Cancel() { }
        public void Dispose() { }
    }

    // ---- Timer tools --------------------------------------------------------

    [Fact]
    public async Task StartTimerTool_StartsTimer_AndConfirms()
    {
        var clock = new FakeTimeProvider();
        var service = new TimerService(clock);
        IVoiceTool tool = TimerTools.CreateStartTimer(service);

        string result = await tool.ExecuteAsync(
            Args("{\"name\":\"tea\",\"seconds\":180}"), CancellationToken.None);

        ActiveTimer timer = Assert.Single(service.ActiveTimers);
        Assert.Equal("tea", timer.Name);
        Assert.Equal(TimeSpan.FromSeconds(180), timer.Remaining);
        Assert.Contains("tea", result);
    }

    [Fact]
    public async Task StartTimerTool_RejectsNonPositiveSeconds_AsArgumentError()
    {
        var service = new TimerService(new FakeTimeProvider());
        IVoiceTool tool = TimerTools.CreateStartTimer(service);

        await Assert.ThrowsAsync<VoiceToolArgumentException>(() =>
            tool.ExecuteAsync(Args("{\"name\":\"tea\",\"seconds\":0}"), CancellationToken.None));
    }

    [Fact]
    public async Task CancelTimerTool_CancelsExisting_AndReportsUnknown()
    {
        var service = new TimerService(new FakeTimeProvider());
        service.StartTimer("tea", TimeSpan.FromMinutes(1));
        IVoiceTool tool = TimerTools.CreateCancelTimer(service);

        string cancelled = await tool.ExecuteAsync(Args("{\"name\":\"tea\"}"), CancellationToken.None);
        Assert.Empty(service.ActiveTimers);
        Assert.Contains("tea", cancelled);

        string unknown = await tool.ExecuteAsync(Args("{\"name\":\"tea\"}"), CancellationToken.None);
        Assert.Contains("No active timer", unknown);
    }

    [Fact]
    public async Task ListTimersTool_ListsRemainingTime_OrEmptyMessage()
    {
        var clock = new FakeTimeProvider();
        var service = new TimerService(clock);
        IVoiceTool tool = TimerTools.CreateListTimers(service);

        string empty = await tool.ExecuteAsync(Args("{}"), CancellationToken.None);
        Assert.Contains("No active timers", empty);

        service.StartTimer("tea", TimeSpan.FromMinutes(3));
        string listed = await tool.ExecuteAsync(Args("{}"), CancellationToken.None);
        Assert.Contains("tea", listed);
        Assert.Contains("3:00", listed);
    }

    // ---- Session tools ------------------------------------------------------

    [Fact]
    public async Task GetTimeTool_ReportsClockTime()
    {
        var clock = new FakeTimeProvider(); // fixed at 2026-01-01 12:00:00 UTC
        IVoiceTool tool = SessionTools.CreateGetCurrentTime(clock);

        string result = await tool.ExecuteAsync(Args("{}"), CancellationToken.None);

        Assert.Contains("2026", result);
        Assert.Contains("12:00", result);
    }

    [Fact]
    public async Task SetMuteTool_TogglesSynthesizerMute()
    {
        var tts = new FakeTextToSpeech();
        IVoiceTool tool = SessionTools.CreateSetSpeechMuted(tts);

        string muted = await tool.ExecuteAsync(Args("{\"muted\":true}"), CancellationToken.None);
        Assert.True(tts.IsMuted);
        Assert.Contains("muted", muted, StringComparison.OrdinalIgnoreCase);

        string unmuted = await tool.ExecuteAsync(Args("{\"muted\":false}"), CancellationToken.None);
        Assert.False(tts.IsMuted);
        Assert.Contains("unmuted", unmuted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearConversationTool_EmptiesHistory()
    {
        var history = new ConversationHistory();
        history.AddUserTurn("hi");
        history.AddAssistantTurn("Hello.");
        IVoiceTool tool = SessionTools.CreateClearConversation(history);

        string result = await tool.ExecuteAsync(Args("{}"), CancellationToken.None);

        Assert.Equal(0, history.Count);
        Assert.Contains("cleared", result, StringComparison.OrdinalIgnoreCase);
    }
}
