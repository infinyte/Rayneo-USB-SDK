// -----------------------------------------------------------------------------
// WhisperSpeechToTextTests.cs
// Author: Kurt Mitchell
//
// The Whisper orchestrator against fakes for the microphone and the transcriber:
// the full push-to-talk contract — capture lifecycle, idempotent Start/Stop,
// exactly-one-final, empty-on-silence, periodic partials with single-flight and
// post-Stop suppression, fault handling, turn reuse, and disposal. The fake
// transcriber completes via TaskCompletionSource so tests control timing exactly
// — no sleeps, no real clocks, no microphone, model, or network.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infinyte.RayNeo.Hud.Voice;

namespace RayNeo.Hud.Tests;

public sealed class WhisperSpeechToTextTests
{
    // ---- Fakes --------------------------------------------------------------

    private sealed class FakeAudioCaptureSource : IAudioCaptureSource
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public event EventHandler<float[]>? SamplesAvailable;
        public event EventHandler<Exception>? Failed;

        public void Start() => StartCalls++;
        public void Stop() => StopCalls++;
        public void Dispose() => DisposeCalls++;

        public void RaiseSamples(float[] samples) => SamplesAvailable?.Invoke(this, samples);
        public void RaiseFailed(Exception ex) => Failed?.Invoke(this, ex);
    }

    // Records every call's samples and hands back a task the test completes when
    // it chooses. Continuations run synchronously on SetResult, so once a test
    // completes a call the resulting event has already fired.
    private sealed class FakeWhisperTranscriber : IWhisperTranscriber
    {
        private readonly List<TaskCompletionSource<string>> _pending = new();

        public List<float[]> Calls { get; } = new();
        public int DisposeCalls { get; private set; }
        public int CallCount => Calls.Count;

        public Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<string>();
            Calls.Add(samples);
            _pending.Add(tcs);
            return tcs.Task;
        }

        public void Complete(int index, string text) => _pending[index].TrySetResult(text);

        public void Fail(int index, Exception ex) => _pending[index].TrySetException(ex);

        public void Dispose() => DisposeCalls++;
    }

    // ---- Helpers ------------------------------------------------------------

    private sealed record Harness(
        WhisperSpeechToText Stt,
        FakeAudioCaptureSource Capture,
        FakeWhisperTranscriber Transcriber,
        List<string> Partials,
        List<string> Finals,
        List<Exception> Failures);

    private static Harness Build(int partialIntervalSamples = 1_000, int minFinalSamples = 5)
    {
        var capture = new FakeAudioCaptureSource();
        var transcriber = new FakeWhisperTranscriber();
        var stt = new WhisperSpeechToText(capture, transcriber, partialIntervalSamples, minFinalSamples);

        var partials = new List<string>();
        var finals = new List<string>();
        var failures = new List<Exception>();
        stt.PartialRecognized += (_, t) => partials.Add(t);
        stt.FinalRecognized += (_, t) => finals.Add(t);
        stt.Failed += (_, ex) => failures.Add(ex);

        return new Harness(stt, capture, transcriber, partials, finals, failures);
    }

    private static float[] Block(int count) => new float[count];

    // Partial and final transcriptions complete on background tasks, so their
    // events surface asynchronously. Poll (bounded) for the expected state rather
    // than sleeping a fixed interval — the same shape the voice-controller tests use.
    private static void WaitFor(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the expected async event.");
            }
            Thread.Sleep(1);
        }
    }

    // ---- Capture lifecycle --------------------------------------------------

    [Fact]
    public void StartBeginsCapture()
    {
        Harness h = Build();
        h.Stt.Start();
        Assert.Equal(1, h.Capture.StartCalls);
    }

    [Fact]
    public void StartWhileCapturingIsNoOp()
    {
        Harness h = Build();
        h.Stt.Start();
        h.Stt.Start();
        Assert.Equal(1, h.Capture.StartCalls);
    }

    [Fact]
    public void StopWithoutStartIsNoOp()
    {
        Harness h = Build();
        h.Stt.Stop();
        Assert.Equal(0, h.Capture.StopCalls);
        Assert.Empty(h.Finals);
        Assert.Empty(h.Failures);
        Assert.Equal(0, h.Transcriber.CallCount);
    }

    // ---- Final transcript ---------------------------------------------------

    [Fact]
    public void StopEmitsExactlyOneFinalTranscript()
    {
        Harness h = Build(partialIntervalSamples: 1_000, minFinalSamples: 5);
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(50));
        h.Stt.Stop();

        Assert.Equal(1, h.Capture.StopCalls);
        Assert.Equal(1, h.Transcriber.CallCount);
        Assert.Equal(50, h.Transcriber.Calls[0].Length); // saw the full buffer

        h.Transcriber.Complete(0, "hello world");
        WaitFor(() => h.Finals.Count == 1);
        Assert.Equal(new[] { "hello world" }, h.Finals);
        Assert.Empty(h.Failures);
    }

    [Fact]
    public void StopWithNoAudioEmitsEmptyFinal()
    {
        Harness h = Build(partialIntervalSamples: 1_000, minFinalSamples: 5);
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(3)); // below the final threshold
        h.Stt.Stop();

        Assert.Equal(new[] { string.Empty }, h.Finals);
        Assert.Equal(0, h.Transcriber.CallCount); // Whisper never runs on near-silence
    }

    // ---- Partial transcripts ------------------------------------------------

    [Fact]
    public void PartialEmittedAfterThresholdAudio()
    {
        Harness h = Build(partialIntervalSamples: 10, minFinalSamples: 5);
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(10)); // reaches the partial interval

        Assert.Equal(1, h.Transcriber.CallCount);
        h.Transcriber.Complete(0, "live text");
        WaitFor(() => h.Partials.Count == 1);
        Assert.Equal(new[] { "live text" }, h.Partials);
    }

    [Fact]
    public void SecondPartialCoversWholeBuffer()
    {
        Harness h = Build(partialIntervalSamples: 10, minFinalSamples: 5);
        h.Stt.Start();

        h.Capture.RaiseSamples(Block(10));
        h.Transcriber.Complete(0, "first");
        WaitFor(() => h.Partials.Count == 1); // pass 0 finished; single-flight cleared
        h.Capture.RaiseSamples(Block(10)); // buffer now 20; another interval of new audio

        Assert.Equal(2, h.Transcriber.CallCount);
        Assert.Equal(10, h.Transcriber.Calls[0].Length);
        Assert.Equal(20, h.Transcriber.Calls[1].Length); // whole accumulated buffer
    }

    [Fact]
    public void OnlyOnePartialPassInFlight()
    {
        Harness h = Build(partialIntervalSamples: 10, minFinalSamples: 5);
        h.Stt.Start();

        h.Capture.RaiseSamples(Block(10)); // launches pass 0 (left in flight)
        h.Capture.RaiseSamples(Block(10)); // more audio while pass 0 pending
        h.Capture.RaiseSamples(Block(10));

        Assert.Equal(1, h.Transcriber.CallCount); // no concurrent second pass
    }

    [Fact]
    public void PartialCompletingAfterStopIsSuppressed()
    {
        // minFinal above the buffer so Stop emits empty without a final pass —
        // isolating the stale-partial suppression path.
        Harness h = Build(partialIntervalSamples: 10, minFinalSamples: 50);
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(10)); // launches partial pass 0
        h.Stt.Stop();                       // buffer < minFinal → empty final
        WaitFor(() => h.Finals.Count == 1);
        Assert.Equal(new[] { string.Empty }, h.Finals);

        h.Transcriber.Complete(0, "stale hypothesis"); // arrives after Stop

        // Prove the stale hypothesis never surfaces: run a fresh turn's partial to
        // completion. The stale pass's generation no longer matches, so whenever it
        // runs it is suppressed — the only partial ever raised is the fresh one.
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(10)); // launches partial pass 1 (new generation)
        h.Transcriber.Complete(1, "fresh hypothesis");
        WaitFor(() => h.Partials.Count == 1);

        Assert.Equal(new[] { "fresh hypothesis" }, h.Partials);
    }

    // ---- Faults -------------------------------------------------------------

    [Fact]
    public void TranscriberErrorRaisesFailedNotFinal()
    {
        Harness h = Build(partialIntervalSamples: 1_000, minFinalSamples: 5);

        h.Stt.Start();
        h.Capture.RaiseSamples(Block(50));
        h.Stt.Stop();
        var boom = new InvalidOperationException("whisper blew up");
        h.Transcriber.Fail(0, boom);
        WaitFor(() => h.Failures.Count == 1);

        Assert.Same(boom, Assert.Single(h.Failures));
        Assert.Empty(h.Finals);

        // The next turn still works.
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(50));
        h.Stt.Stop();
        h.Transcriber.Complete(1, "recovered");
        WaitFor(() => h.Finals.Count == 1);
        Assert.Equal(new[] { "recovered" }, h.Finals);
    }

    [Fact]
    public void CaptureFailureRaisesFailed()
    {
        Harness h = Build();
        h.Stt.Start();

        var fault = new InvalidOperationException("mic unplugged");
        h.Capture.RaiseFailed(fault);

        Assert.Same(fault, Assert.Single(h.Failures));

        // State reset: a fresh turn can start.
        h.Stt.Start();
        Assert.Equal(2, h.Capture.StartCalls);
    }

    // ---- Reuse and disposal -------------------------------------------------

    [Fact]
    public void SecondTurnStartsWithEmptyBuffer()
    {
        Harness h = Build(partialIntervalSamples: 1_000, minFinalSamples: 5);

        h.Stt.Start();
        h.Capture.RaiseSamples(Block(20));
        h.Stt.Stop();
        h.Transcriber.Complete(0, "turn one");

        h.Stt.Start();
        h.Capture.RaiseSamples(Block(30));
        h.Stt.Stop();

        Assert.Equal(2, h.Transcriber.CallCount);
        Assert.Equal(20, h.Transcriber.Calls[0].Length);
        Assert.Equal(30, h.Transcriber.Calls[1].Length); // only turn two's audio
    }

    [Fact]
    public void DisposeStopsCaptureAndSuppressesEvents()
    {
        Harness h = Build(partialIntervalSamples: 1_000, minFinalSamples: 5);
        h.Stt.Start();
        h.Capture.RaiseSamples(Block(50));

        h.Stt.Dispose();

        Assert.Equal(1, h.Capture.DisposeCalls);
        Assert.True(h.Capture.StopCalls >= 1);
        Assert.Equal(1, h.Transcriber.DisposeCalls);

        // Post-dispose input is ignored.
        h.Capture.RaiseSamples(Block(50));
        Assert.Empty(h.Finals);
        Assert.Empty(h.Partials);
    }

    [Fact]
    public void StartAfterDisposeIsNoOp()
    {
        Harness h = Build();
        h.Stt.Dispose();
        h.Stt.Start();
        Assert.Equal(0, h.Capture.StartCalls);
    }
}
