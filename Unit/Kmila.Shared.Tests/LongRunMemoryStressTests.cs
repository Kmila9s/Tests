using Interpreter.Services;
using Kmila.Shared.Models;
using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Long-running freeze + crash repro (headless).
//
// Field reports: during a LONG UI simulation the app freezes and then crashes —
// on Android three crashes were captured on GC / thread-pool threads (a Mono
// SGen Large-Object-Space assertion and a heap-corruption SIGSEGV), and on
// desktop/web it shows as a multi-GB memory blow-up. Root cause: the UI run
// path RETAINS ever-growing per-transition / per-frame state (SignalHistory,
// WaveformData.Transitions, ReplayState.LiveDoc) and CHURNS large objects by
// copying the whole signal table every frame. Headless short runs don't show
// it, so this test forces the real pipeline to run long and asserts the three
// things that must stay bounded.
//
// These tests are RED on the unfixed tree and become GREEN as each fix lands:
//   - SignalHistory per-signal cap            (Interpreter, protected)
//   - WaveformData live-Transitions cap        (Kmila.Shared)
//   - ReplayState delta frames + O(1) eviction (Kmila.Shared)
//
// Drives the real SimulationRunner + ReplayState + WaveformData pipeline — the
// exact code the Editor live-run uses — with no UI.
// ─────────────────────────────────────────────────────────────────────────────
public class LongRunMemoryStressTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "Stress");

    // Clock-edge counts for the two-length ratio checks. Both must exceed the
    // per-signal retention cap so a bounded implementation plateaus (ratio ~1)
    // while the unbounded one scales linearly (ratio ~2).
    private const int EdgesSmall = 6000;
    private const int EdgesLarge = 12000;

    private sealed record StressResult(
        int HistoryTotalSamples,
        int SignalCount,
        int WaveformTotalTransitions,
        int ReplayStepCount,
        double ReplayLateAvgSignalsPerStep);

    // Parse the stress fixture and run it for `clockEdges` rising edges through
    // the reference SimulationRunner, optionally with a live ReplayState attached
    // (the UI editor path). All metrics are captured BEFORE the runner is
    // disposed and returned as scalars so disposal can't affect them.
    private static async Task<StressResult> RunStressAsync(int clockEdges, bool attachReplay)
    {
        EntityRegistry.Instance.Clear();
        PackageRegistry.Instance.Clear();
        FunctionRegistry.Instance.Clear();
        SignalAttributeHandler.Instance.Reset();

        var source = File.ReadAllText(Path.Combine(FixtureDir, "self_retriggering_clock.vhd"));
        var path = Path.Combine(Path.GetTempPath(), $"kmila_stress_{Guid.NewGuid():N}.vhd");
        await File.WriteAllTextAsync(path, source);
        try
        {
            EntityRegistry.Instance.ScanFile(path);

            var parser = new Debugger.Repositories.Parser(path);
            parser.ParseFile();
            var entity = parser.Entities.FirstOrDefault(e => e.Architecture != null);
            Assert.NotNull(entity);

            const int tickResNs = 10;
            const int clockMhz = 50;                 // 20 ns period → rising edge every 20 ns
            long totalNs = (long)clockEdges * 20L;

            var config = StimulusBuilder.BuildConfigNs(entity!, portMoments: null,
                totalNs: totalNs, tickResNs: tickResNs, systemClockMhz: clockMhz);

            var replay = new ReplayState(new SignalCursorBus());
            if (attachReplay) replay.LiveReplayDuringRun = true;

            // WaveformData: the second, UI-facing copy of the history. The Editor
            // rebuilds it incrementally from a 200 ms progress timer, so we mirror
            // that here — merge periodically DURING the run (not once at the end),
            // which is what makes WaveformData.Transitions accumulate cumulatively
            // in the live path.
            var wf = WaveformData.CreateEmpty(tickResNs);
            int frameCounter = 0;

            var runner = new SimulationRunner();
            try
            {
                runner.OnEngineReady += eng =>
                {
                    eng.TickResolutionNs = tickResNs;
                    if (attachReplay) replay.AttachLive(eng, source);
                    eng.OnFrameEmitted += _ =>
                    {
                        // ~ one merge per 500 frames ≈ the real progress-timer cadence.
                        if (++frameCounter % 500 == 0)
                        {
                            lock (eng.History) { wf.MergeFrom(eng.History); }
                        }
                    };
                };
                await runner.RunAsync(config);

                var history = runner.Engine!.History;
                lock (history) { wf.MergeFrom(history); }   // final catch-up merge
                int wfTransitions = wf.Signals.Sum(s => s.Transitions.Count);

                int replaySteps = 0;
                double lateAvg = 0;
                var doc = attachReplay ? replay.LiveDoc : null;
                if (doc != null)
                {
                    replaySteps = doc.Steps.Count;
                    // Average per-step payload size over the LATE window (by then
                    // every signal has been seen at least once, so a full-table
                    // snapshot == SignalCount while a delta == a handful).
                    int take = Math.Min(200, doc.Steps.Count);
                    if (take > 0)
                        lateAvg = doc.Steps.Skip(doc.Steps.Count - take)
                                           .Average(s => s.Signals.Count);
                }

                return new StressResult(
                    history.TotalSamples, history.SignalCount, wfTransitions,
                    replaySteps, lateAvg);
            }
            finally
            {
                runner.Dispose();
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort temp cleanup */ }
        }
    }

    // #1 — SignalHistory must not grow linearly with run length.
    [Fact]
    public async Task SignalHistory_LongRun_StaysBounded()
    {
        var small = await RunStressAsync(EdgesSmall, attachReplay: false);
        var large = await RunStressAsync(EdgesLarge, attachReplay: false);

        Assert.True(small.HistoryTotalSamples > 0, "fixture produced no history");
        Assert.True(
            large.HistoryTotalSamples <= small.HistoryTotalSamples * 1.6,
            $"SignalHistory grows ~linearly with run length (unbounded): " +
            $"{small.HistoryTotalSamples} samples at {EdgesSmall} edges → " +
            $"{large.HistoryTotalSamples} at {EdgesLarge}. A bounded cap should plateau.");
    }

    // #3 — the UI waveform copy must not grow linearly either.
    [Fact]
    public async Task WaveformData_LiveMerge_StaysBounded()
    {
        var small = await RunStressAsync(EdgesSmall, attachReplay: false);
        var large = await RunStressAsync(EdgesLarge, attachReplay: false);

        Assert.True(small.WaveformTotalTransitions > 0, "fixture produced no transitions");
        Assert.True(
            large.WaveformTotalTransitions <= small.WaveformTotalTransitions * 1.6,
            $"WaveformData.Transitions grows ~linearly (unbounded): " +
            $"{small.WaveformTotalTransitions} at {EdgesSmall} edges → " +
            $"{large.WaveformTotalTransitions} at {EdgesLarge}.");
    }

    // #4a — the live-replay doc must stay within its hard cap.
    [Fact]
    public async Task ReplayDoc_LongRun_StaysWithinCap()
    {
        var r = await RunStressAsync(EdgesLarge, attachReplay: true);

        Assert.True(r.ReplayStepCount > 0, "no live-replay frames accumulated");
        Assert.True(
            r.ReplayStepCount <= ReplayState.LiveDocFrameCap,
            $"live-replay doc exceeded its cap: {r.ReplayStepCount} > {ReplayState.LiveDocFrameCap}");
    }

    // #4b — each live-replay frame stores a DELTA (changed signals), not a full
    // copy of the whole signal table (the per-frame full-table copy was
    // large-object churn). Phase 2: delta steps + a keyframe every
    // ReplayState.LiveKeyframeInterval steps; ExecutionTimelineDoc.ResolveSignals
    // reconstructs full state, and the sim-thread writer / UI-thread reader race
    // is closed by SyncRoot + ReadView().
    [Fact]
    public async Task ReplayDoc_Frames_AreDeltas_NotFullTableSnapshots()
    {
        var r = await RunStressAsync(EdgesLarge, attachReplay: true);

        Assert.True(r.ReplayStepCount > 0, "no live-replay frames accumulated");
        Assert.True(r.SignalCount >= 6, $"expected a multi-signal design, saw {r.SignalCount}");
        Assert.True(
            r.ReplayLateAvgSignalsPerStep < r.SignalCount * 0.5,
            $"live-replay frames store the FULL signal table " +
            $"(avg {r.ReplayLateAvgSignalsPerStep:F1} of {r.SignalCount} signals per frame) " +
            $"instead of per-frame deltas — this is the large-object churn that crashes long runs.");
    }

    // #4c — delta frames must reconstruct EXACTLY the full table the old per-frame
    // snapshot stored, including steps that survived FIFO eviction (whose first
    // step was promoted to a keyframe). An independent observer subscribed after
    // ReplayState rebuilds the running table frame by frame and samples it; every
    // sample still on the tape must match ExecutionTimelineDoc.ResolveSignals.
    [Fact]
    public async Task ReplayDoc_DeltaFrames_ReconstructFullTable()
    {
        EntityRegistry.Instance.Clear();
        PackageRegistry.Instance.Clear();
        FunctionRegistry.Instance.Clear();
        SignalAttributeHandler.Instance.Reset();

        var source = File.ReadAllText(Path.Combine(FixtureDir, "self_retriggering_clock.vhd"));
        var path = Path.Combine(Path.GetTempPath(), $"kmila_stress_{Guid.NewGuid():N}.vhd");
        await File.WriteAllTextAsync(path, source);
        try
        {
            EntityRegistry.Instance.ScanFile(path);
            var parser = new Debugger.Repositories.Parser(path);
            parser.ParseFile();
            var entity = parser.Entities.FirstOrDefault(e => e.Architecture != null);
            Assert.NotNull(entity);

            const int tickResNs = 10;
            var config = StimulusBuilder.BuildConfigNs(entity!, portMoments: null,
                totalNs: (long)EdgesLarge * 20L, tickResNs: tickResNs, systemClockMhz: 50);

            var replay = new ReplayState(new SignalCursorBus()) { LiveReplayDuringRun = true };
            var running = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var expected = new Dictionary<long, Dictionary<string, string>>(); // 1-based frame → table
            long frameNo = 0;

            var runner = new SimulationRunner();
            try
            {
                runner.OnEngineReady += eng =>
                {
                    eng.TickResolutionNs = tickResNs;
                    replay.AttachLive(eng, source); // subscribes first → writes the frame first
                    eng.OnFrameEmitted += args =>
                    {
                        foreach (var s in args.ChangedSignals)
                            if (!string.IsNullOrEmpty(s.Name))
                                running[s.Name] = s.Value?.ToString() ?? "U";
                        frameNo++;
                        if (frameNo % 37 == 0)
                            expected[frameNo] = new Dictionary<string, string>(running, StringComparer.OrdinalIgnoreCase);
                    };
                };
                await runner.RunAsync(config);

                var view = replay.LiveDoc!.ReadView();
                Assert.Equal(frameNo, (long)replay.LiveFrameTick);
                Assert.True(frameNo > ReplayState.LiveDocFrameCap,
                    $"run too short to exercise eviction ({frameNo} frames)");
                Assert.False(view.Steps[0].IsDelta, "first step after eviction must be a keyframe");

                long firstFrame = frameNo - view.Steps.Count + 1;
                int compared = 0;
                foreach (var (f, exp) in expected)
                {
                    long idx = f - firstFrame;
                    if (idx < 0 || idx >= view.Steps.Count) continue;
                    var got = view.ResolveSignals((int)idx);
                    Assert.Equal(exp.Count, got.Count);
                    foreach (var kv in exp)
                    {
                        Assert.True(got.TryGetValue(kv.Key, out var gv), $"frame {f}: missing {kv.Key}");
                        Assert.Equal(kv.Value, gv);
                    }
                    compared++;
                }
                Assert.True(compared > 50, $"too few samples compared ({compared})");
            }
            finally
            {
                runner.Dispose();
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort temp cleanup */ }
        }
    }
}
