// -----------------------------------------------------------------------------
// VoiceInteractionController.cs
// Author: Kurt Mitchell
//
// The orchestrator of the hands-free voice loop. It owns the state machine and
// drives it from four engine-agnostic collaborators — push-to-talk, speech
// recognition, the assistant client, and speech synthesis — plus the optional
// timer service for expiry announcements. All engine specifics live behind the
// interfaces, so this class (and every branch of the loop) is unit-tested with
// fakes and no audio, network, or UI.
//
// Threading: collaborator events arrive on arbitrary threads (keyboard hook,
// recognizer, timer, streaming task). All state transitions happen under one
// gate, and the controller's own events may therefore fire on any thread — the
// HUD marshals to its dispatcher (CLAUDE.md Phase 3: every state change must
// be visible on the glasses).
// -----------------------------------------------------------------------------

using System.Text;

namespace Infinyte.RayNeo.Voice;

/// <summary>Runs the voice loop: Idle → Listening → … → Idle, with barge-in.</summary>
public sealed class VoiceInteractionController : IDisposable
{
    private readonly IPushToTalkSource _pushToTalk;
    private readonly ISpeechToText _speechToText;
    private readonly IAssistantClient _assistant;
    private readonly ITextToSpeech _textToSpeech;
    private readonly ConversationHistory _history;
    private readonly TimerService? _timers;
    private readonly VoiceInteractionStateMachine _machine = new();
    private readonly object _gate = new();

    private CancellationTokenSource? _replyCts;
    private Task _replyTask = Task.CompletedTask;
    private bool _disposed;

    /// <summary>Creates the controller over its collaborators.</summary>
    /// <param name="timers">Optional timer service whose expirations are announced.</param>
    public VoiceInteractionController(
        IPushToTalkSource pushToTalk,
        ISpeechToText speechToText,
        IAssistantClient assistant,
        ITextToSpeech textToSpeech,
        ConversationHistory history,
        TimerService? timers = null)
    {
        _pushToTalk = pushToTalk ?? throw new ArgumentNullException(nameof(pushToTalk));
        _speechToText = speechToText ?? throw new ArgumentNullException(nameof(speechToText));
        _assistant = assistant ?? throw new ArgumentNullException(nameof(assistant));
        _textToSpeech = textToSpeech ?? throw new ArgumentNullException(nameof(textToSpeech));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _timers = timers;
    }

    /// <summary>The loop's current state.</summary>
    public VoiceState CurrentState => _machine.CurrentState;

    /// <summary>Raised on every state transition (may fire on any thread).</summary>
    public event EventHandler<VoiceStateChangedEventArgs>? StateChanged;

    /// <summary>Live partial transcript while the wearer is speaking.</summary>
    public event EventHandler<string>? PartialTranscript;

    /// <summary>A chunk of the assistant's reply, in arrival order.</summary>
    public event EventHandler<string>? ReplyDelta;

    /// <summary>Tool activity forwarded from the assistant (for HUD toasts).</summary>
    public event EventHandler<ToolActivityEventArgs>? ToolActivity;

    /// <summary>A timer expired; the string is the announcement text.</summary>
    public event EventHandler<string>? TimerAnnouncement;

    /// <summary>A recoverable error occurred and the loop returned to Idle.</summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>Subscribes to all collaborators and begins monitoring push-to-talk.</summary>
    public void Start()
    {
        _machine.StateChanged += OnMachineStateChanged;
        _pushToTalk.Pressed += OnPushToTalkPressed;
        _pushToTalk.Released += OnPushToTalkReleased;
        _speechToText.PartialRecognized += OnPartialRecognized;
        _speechToText.FinalRecognized += OnFinalRecognized;
        _speechToText.Failed += OnRecognitionFailed;
        _textToSpeech.SpeakCompleted += OnSpeakCompleted;
        if (_assistant is ClaudeAssistantClient claude)
        {
            claude.ToolActivity += OnToolActivity;
        }
        if (_timers is not null)
        {
            _timers.TimerExpired += OnTimerExpired;
        }
        _pushToTalk.Start();
    }

    /// <summary>
    /// Completes when no reply task is in flight. Lets tests (and shutdown)
    /// deterministically wait out an in-flight or just-cancelled reply.
    /// </summary>
    public Task WaitForReplyTaskAsync() => _replyTask;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _pushToTalk.Pressed -= OnPushToTalkPressed;
        _pushToTalk.Released -= OnPushToTalkReleased;
        _speechToText.PartialRecognized -= OnPartialRecognized;
        _speechToText.FinalRecognized -= OnFinalRecognized;
        _speechToText.Failed -= OnRecognitionFailed;
        _textToSpeech.SpeakCompleted -= OnSpeakCompleted;
        if (_assistant is ClaudeAssistantClient claude)
        {
            claude.ToolActivity -= OnToolActivity;
        }
        if (_timers is not null)
        {
            _timers.TimerExpired -= OnTimerExpired;
        }
        lock (_gate)
        {
            _replyCts?.Cancel();
            _replyCts?.Dispose();
            _replyCts = null;
        }
    }

    // ---- Push-to-talk -------------------------------------------------------

    private void OnPushToTalkPressed(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            VoiceState before = _machine.CurrentState;
            if (!_machine.TryFire(VoiceTrigger.PushToTalkPressed, out _))
            {
                return; // stray press (already Listening / Transcribing)
            }
            if (before is VoiceState.Thinking or VoiceState.Streaming or VoiceState.Speaking)
            {
                // Barge-in: abandon the in-flight reply and stop any playback.
                _replyCts?.Cancel();
                _textToSpeech.Cancel();
            }
            _speechToText.Start();
        }
    }

    private void OnPushToTalkReleased(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_machine.TryFire(VoiceTrigger.PushToTalkReleased, out _))
            {
                _speechToText.Stop();
            }
        }
    }

    // ---- Recognition --------------------------------------------------------

    private void OnPartialRecognized(object? sender, string text)
    {
        if (_machine.CurrentState == VoiceState.Listening)
        {
            PartialTranscript?.Invoke(this, text);
        }
    }

    private void OnFinalRecognized(object? sender, string text)
    {
        lock (_gate)
        {
            if (_machine.CurrentState != VoiceState.Transcribing)
            {
                return; // stale finalization (barge-in or fault already moved on)
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                _machine.TryFire(VoiceTrigger.TranscriptEmpty, out _);
                return;
            }
            _history.AddUserTurn(text);
            _machine.TryFire(VoiceTrigger.TranscriptRecognized, out _);

            _replyCts?.Dispose();
            _replyCts = new CancellationTokenSource();
            _replyTask = RunReplyAsync(_replyCts.Token);
        }
    }

    private void OnRecognitionFailed(object? sender, Exception exception) =>
        Fault(exception.Message);

    // ---- Reply streaming ----------------------------------------------------

    // Streams one assistant reply. Cancellation (barge-in) is silent: the state
    // machine has already moved to Listening. Any other failure faults to Idle.
    private async Task RunReplyAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ConversationTurn> snapshot = _history.Turns;
        var reply = new StringBuilder();
        try
        {
            await foreach (string delta in _assistant.StreamReplyAsync(snapshot, cancellationToken)
                .ConfigureAwait(false))
            {
                lock (_gate)
                {
                    if (reply.Length == 0 &&
                        !_machine.TryFire(VoiceTrigger.ResponseStarted, out _))
                    {
                        return; // barge-in won the race before the first token
                    }
                    reply.Append(delta);
                }
                ReplyDelta?.Invoke(this, delta);
            }
            cancellationToken.ThrowIfCancellationRequested();
            CompleteReply(reply.ToString());
        }
        catch (OperationCanceledException)
        {
            // Barge-in; the partial reply is discarded by design.
        }
        catch (Exception ex)
        {
            Fault(ex.Message);
        }
    }

    private void CompleteReply(string reply)
    {
        lock (_gate)
        {
            if (_machine.CurrentState == VoiceState.Thinking &&
                !_machine.TryFire(VoiceTrigger.ResponseStarted, out _))
            {
                return; // zero-delta reply raced a barge-in
            }
            if (string.IsNullOrWhiteSpace(reply))
            {
                _machine.TryFire(VoiceTrigger.ResponseCompletedSilently, out _);
                return;
            }
            if (_textToSpeech.IsMuted)
            {
                if (_machine.TryFire(VoiceTrigger.ResponseCompletedSilently, out _))
                {
                    _history.AddAssistantTurn(reply);
                }
                return;
            }
            if (_machine.TryFire(VoiceTrigger.ResponseCompletedWithSpeech, out _))
            {
                _history.AddAssistantTurn(reply);
                _textToSpeech.SpeakAsync(reply);
            }
        }
    }

    // ---- Synthesis / tools / timers ----------------------------------------

    private void OnSpeakCompleted(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            _machine.TryFire(VoiceTrigger.SpeechCompleted, out _); // no-op unless Speaking
        }
    }

    private void OnToolActivity(object? sender, ToolActivityEventArgs e) =>
        ToolActivity?.Invoke(this, e);

    private void OnTimerExpired(object? sender, string name)
    {
        string announcement = $"Timer '{name}' is done.";
        TimerAnnouncement?.Invoke(this, announcement);
        lock (_gate)
        {
            // Speak only when the floor is free; the HUD announcement always shows.
            if (_machine.CurrentState == VoiceState.Idle && !_textToSpeech.IsMuted)
            {
                _textToSpeech.SpeakAsync(announcement);
            }
        }
    }

    private void Fault(string message)
    {
        lock (_gate)
        {
            _machine.TryFire(VoiceTrigger.Fault, out _);
        }
        ErrorOccurred?.Invoke(this, message);
    }

    private void OnMachineStateChanged(object? sender, VoiceStateChangedEventArgs e) =>
        StateChanged?.Invoke(this, e);
}
