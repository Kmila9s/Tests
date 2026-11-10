using Interpreter.Models;
using Interpreter.Services;
using Kmila.Shared.Models;
using Kmila.Shared.Services;
using Kmila.Shared.Services.Trace;

namespace Kmila.Shared.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Long-run safety net: RunWatchdog stops a run GRACEFULLY (same path as the Stop
// button) before it exhausts trace storage or memory.
//
// The contract these tests pin:
//   - Policy: trace near its limit → TraceStorageLimit; OS critical pressure →
//     MemoryPressure; heap over budget only if it STAYS over after a collection.
//   - Stopping at the trace soft limit keeps the archive COMPLETE (not truncated)
//     and it matches the same design run uncapped, transition for transition.
//   - ProgramBuilder end to end: the watchdog's stop surfaces as
//     OnRunWatchdogStopped + a normal stopped run with a partial waveform.
// ─────────────────────────────────────────────────────────────────────────────
public class RunWatchdogTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Stress");
    private const int TickResNs = 10;

    // ── Policy ───────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_CriticalPressure_StopsOnlyRunsStartedBeforeIt()
    {
        var wd = new RunWatchdog(maxManagedHeapBytes: -1); // heap check off
        long before = RunWatchdog.PressureStamp;
        Assert.Equal(RunStopReason.None, wd.Evaluate(null, before, 0, () => 0));

        RunWatchdog.ReportCriticalMemoryPressure();
        Assert.Equal(RunStopReason.MemoryPressure, wd.Evaluate(null, before, 0, () => 0));
        // A run that starts after the signal is not affected by it.
        Assert.Equal(RunStopReason.None, wd.Evaluate(null, RunWatchdog.PressureStamp, 0, () => 0));
    }

    [Fact]
    public void Evaluate_HeapBudget_RequiresHeapToStayOverAfterCollect()
    {
        var wd = new RunWatchdog(maxManagedHeapBytes: 1000);
        long stamp = RunWatchdog.PressureStamp;

        bool collected = false;
        // Under budget: no forced collection at all.
        Assert.Equal(RunStopReason.None, wd.Evaluate(null, stamp, 900, () => { collected = true; return 900; }));
        Assert.False(collected);

        // Over budget, but it was only garbage.
        Assert.Equal(RunStopReason.None, wd.Evaluate(null, stamp, 5000, () => 400));
        // Over budget and live.
        Assert.Equal(RunStopReason.MemoryPressure, wd.Evaluate(null, stamp, 5000, () => 4000));

        // Disabled heap check never stops.
        Assert.Equal(RunStopReason.None,
            new RunWatchdog(maxManagedHeapBytes: -1).Evaluate(null, stamp, long.MaxValue, () => long.MaxValue));
    }

    [Fact]
    public void TraceStorage_SoftLimit_IsBelowHardLimit()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kmila_wd_{Guid.NewGuid():N}");
        try
        {
            var storage = new TraceStorage(dir) { MaxTraceBytes = 1000, SoftLimitFraction = 0.5, MinFreeBytes = 0 };
            Assert.False(storage.IsNearLimit(499));
            Assert.True(storage.IsNearLimit(500));
            Assert.True(storage.IsNearLimit(1001));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    // ── Trace-limit stop keeps the archive complete ──────────────────────────

    private static SimulationConfig BuildConfig(int clockEdges)
    {
        EntityRegistry.Instance.Clear();
        PackageRegistry.Instance.Clear();
        FunctionRegistry.Instance.Clear();
        SignalAttributeHandler.Instance.Reset();

        var path = Path.Combine(Path.GetTempPath(), $"kmila_wd_{Guid.NewGuid():N}.vhd");
        File.WriteAllText(path, File.ReadAllText(Path.Combine(FixtureDir, "self_retriggering_clock.vhd")));
        try
        {
            EntityRegistry.Instance.ScanFile(path);
            var parser = new Debugger.Repositories.Parser(path);
            parser.ParseFile();
            var entity = parser.Entities.First(e => e.Architecture != null);
            return StimulusBuilder.BuildConfigNs(entity, portMoments: null,
                totalNs: (long)clockEdges * 20L, tickResNs: TickResNs, systemClockMhz: 50);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task TraceSoftLimit_StopsRun_ArchiveCompleteAndMatchesUncapped()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kmila_wd_{Guid.NewGuid():N}");
        // Soft limit at 30 KB: well into the run (several evictions spilled), with
        // ample headroom under the 300 KB hard guard for the in-memory tail.
        var storage = new TraceStorage(dir) { MaxTraceBytes = 300_000, SoftLimitFraction = 0.1, MinFreeBytes = 0 };
        var wd = new RunWatchdog(maxManagedHeapBytes: -1) { PollInterval = TimeSpan.FromMilliseconds(10) };
        const int edges = 2_000_000; // far longer than the watchdog lets it run
        TraceSession? trace = null;
        try
        {
            var config = BuildConfig(edges);
            trace = storage.StartSession(TickResNs);
            var runner = new SimulationRunner();
            bool? completed = null;
            RunStopReason reason = RunStopReason.None;
            IDisposable? watch = null;
            runner.OnEngineReady += eng =>
            {
                eng.TickResolutionNs = TickResNs;
                eng.History.MaxSamplesPerSignal = 5000;
                trace.Attach(eng.History);
                watch = wd.Watch(trace, r => { reason = r; runner.Stop(); });
            };
            runner.OnSimulationEnded += c => completed = c;
            await runner.RunAsync(config);
            watch?.Dispose();

            Assert.Equal(RunStopReason.TraceStorageLimit, reason);
            Assert.False(completed);

            var history = runner.Engine!.History;
            Assert.True(await trace.CompleteAsync());
            Assert.False(trace.IsTruncated);
            Assert.Equal(0, trace.PendingSamples);
            var capped = WaveformData.FromSignalHistory(history, TickResNs);
            trace.AttachArchives(capped);
            long stopNs = (long)capped.TotalTicks * TickResNs;
            runner.Dispose();

            // Same design, uncapped, run just past the stop point.
            var fullConfig = BuildConfig((int)(stopNs / 20) + 10);
            var full = new SimulationRunner();
            full.OnEngineReady += eng => { eng.TickResolutionNs = TickResNs; eng.History.MaxSamplesPerSignal = 0; };
            await full.RunAsync(fullConfig);
            var reference = WaveformData.FromSignalHistory(full.Engine!.History, TickResNs);
            full.Dispose();

            foreach (var sig in capped.Signals)
            {
                Assert.NotNull(sig.Archive); // the history really went beyond the RAM window
                var archived = sig.AllTransitions().ToList();
                var expected = reference.Signals.Single(s => s.Name == sig.Name).Transitions
                    .Where(t => t.TimeNs < archived[^1].TimeNs).ToList();
                // Everything before the stop point is present, gap-free and identical.
                Assert.Equal(expected, archived.Take(expected.Count).ToList());
                Assert.Equal(expected.Count + 1, archived.Count);
            }
        }
        finally
        {
            trace?.Dispose();
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    // ── ProgramBuilder end to end ────────────────────────────────────────────

    [Fact]
    public async Task ProgramBuilder_WatchdogStop_SurfacesReasonAndPartialWaveform()
    {
        var project = Path.Combine(Path.GetTempPath(), $"kmila_wdproj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(project);
        File.Copy(Path.Combine(FixtureDir, "self_retriggering_clock.vhd"), Path.Combine(project, "counter.vhd"));

        // A 1-byte heap budget is always exceeded → the first poll stops the run.
        var wd = new RunWatchdog(maxManagedHeapBytes: 1) { PollInterval = TimeSpan.FromMilliseconds(50) };
        using var builder = new ProgramBuilder(libraryCompiler: null, traceStorage: null, watchdog: wd);
        RunStopReason? raised = null;
        bool? ended = null;
        long partialSamples = 0;
        builder.OnRunWatchdogStopped += r => raised = r;
        builder.OnSimulationEnded += c => ended = c;
        builder.OnSimulationCompleted += h => partialSamples = h.TotalSamples;
        try
        {
            EntityRegistry.Instance.Clear();
            SignalAttributeHandler.Instance.Reset();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // 1 s of simulated time = 100M ticks: would take far longer than the test.
            await builder.RunTestbenchAsync(project, "counter.vhd", TimeSpan.FromSeconds(1),
                tickResNs: TickResNs, fastRun: false);
            sw.Stop();

            Assert.Equal(RunStopReason.MemoryPressure, raised);
            Assert.Equal(RunStopReason.MemoryPressure, builder.LastRunStopReason);
            Assert.False(ended);
            Assert.True(partialSamples > 0, "the stopped run must still hand over its partial waveform");
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(60), $"run should stop promptly, took {sw.Elapsed}");
        }
        finally
        {
            try { Directory.Delete(project, true); } catch { /* best effort */ }
        }
    }
}
