using Interpreter.Services;
using Kmila.Shared.Models;
using Kmila.Shared.Services;
using Kmila.Shared.Services.Trace;
using Parser.Models;

namespace Kmila.Shared.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Long-run Phase 2 (stages C+D): history the in-memory cap evicts is spilled to a
// per-run on-disk trace and served back to the waveform as an archive.
//
// The contract these tests pin:
//   - SignalHistory.OnEvicting hands over EXACTLY the evicted samples, in order;
//     evicted ++ retained == the uncapped history (no gap, no overlap).
//   - The archive of a capped run equals, transition for transition, what the
//     in-memory builder produces from an UNCAPPED run of the same design.
//   - Reading through the archive (GetWindow / ValueAt) renders identically to the
//     in-memory path.
//   - Short runs never touch disk; disposing a session deletes its file.
// ─────────────────────────────────────────────────────────────────────────────
public class LongRunTraceStoreTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Stress");
    private const int TickResNs = 10;

    private sealed record RunResult(SignalHistory History, WaveformData Waveform, TraceSession? Trace);

    // Runs the stress fixture for `clockEdges` rising edges with the given history
    // cap (0 = unbounded) and, optionally, a trace session attached the way
    // ProgramBuilder does it. Returns the (post-run) history + waveform.
    private static async Task<RunResult> RunAsync(int clockEdges, int historyCap, TraceStorage? storage,
        Action<Variable, IReadOnlyList<(long, object, int)>>? extraSpill = null)
    {
        EntityRegistry.Instance.Clear();
        PackageRegistry.Instance.Clear();
        FunctionRegistry.Instance.Clear();
        SignalAttributeHandler.Instance.Reset();

        var source = File.ReadAllText(Path.Combine(FixtureDir, "self_retriggering_clock.vhd"));
        var path = Path.Combine(Path.GetTempPath(), $"kmila_trace_{Guid.NewGuid():N}.vhd");
        await File.WriteAllTextAsync(path, source);
        try
        {
            EntityRegistry.Instance.ScanFile(path);
            var parser = new Debugger.Repositories.Parser(path);
            parser.ParseFile();
            var entity = parser.Entities.FirstOrDefault(e => e.Architecture != null);
            Assert.NotNull(entity);
            var config = StimulusBuilder.BuildConfigNs(entity!, portMoments: null,
                totalNs: (long)clockEdges * 20L, tickResNs: TickResNs, systemClockMhz: 50);

            var trace = storage?.StartSession(TickResNs);
            var runner = new SimulationRunner();
            try
            {
                runner.OnEngineReady += eng =>
                {
                    eng.TickResolutionNs = TickResNs;
                    eng.History.MaxSamplesPerSignal = historyCap;
                    trace?.Attach(eng.History);
                    if (extraSpill != null)
                    {
                        var inner = eng.History.OnEvicting;
                        eng.History.OnEvicting = (v, b) => { inner?.Invoke(v, b); extraSpill(v, b); };
                    }
                };
                await runner.RunAsync(config);
                var history = runner.Engine!.History;
                if (trace != null) await trace.CompleteAsync();
                var wf = WaveformData.FromSignalHistory(history, TickResNs);
                trace?.AttachArchives(wf);
                return new RunResult(history, wf, trace);
            }
            finally
            {
                runner.Dispose(); // does not clear the history
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    private static TraceStorage NewStorage(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), $"kmila_traces_{Guid.NewGuid():N}");
        return new TraceStorage(dir);
    }

    // 1 — the hook hands over exactly what is evicted, in order.
    [Fact]
    public async Task OnEvicting_SpillPlusRetained_EqualsUncappedHistory()
    {
        var spilled = new Dictionary<string, List<(long T, string V)>>();
        var capped = await RunAsync(12000, historyCap: 5000, storage: null, extraSpill: (v, batch) =>
        {
            if (!spilled.TryGetValue(v.Name, out var l)) spilled[v.Name] = l = new();
            foreach (var (t, val, _) in batch) l.Add((t, val?.ToString() ?? ""));
        });
        var full = await RunAsync(12000, historyCap: 0, storage: null);

        Assert.NotEmpty(spilled); // the run must actually exceed the cap
        foreach (var sig in full.History.GetTrackedSignals())
        {
            var expected = full.History.GetSignalHistoryDetailed(sig)
                .Select(e => (e.TimeMoment, e.Value?.ToString() ?? "")).ToList();
            var cappedSig = capped.History.GetTrackedSignals().Single(s => s.Name == sig.Name);
            var actual = (spilled.TryGetValue(sig.Name, out var s) ? s : new())
                .Concat(capped.History.GetSignalHistoryDetailed(cappedSig)
                    .Select(e => (e.TimeMoment, e.Value?.ToString() ?? ""))).ToList();
            Assert.Equal(expected, actual);
        }
    }

    // 1b — no hook: eviction behaves exactly as before (bounded, nothing else).
    [Fact]
    public async Task NoHook_CapStillBoundsHistory()
    {
        var r = await RunAsync(12000, historyCap: 5000, storage: null);
        Assert.Null(r.History.OnEvicting);
        Assert.All(r.History.GetTrackedSignals(),
            s => Assert.True(r.History.GetSignalHistoryDetailed(s).Count <= 5000));
    }

    // 2 — archive of a capped run == in-memory build of the uncapped run.
    [Fact]
    public async Task Archive_EqualsUncappedWaveform_TransitionForTransition()
    {
        var storage = NewStorage(out var dir);
        var capped = await RunAsync(12000, historyCap: 5000, storage);
        try
        {
            var full = await RunAsync(12000, historyCap: 0, storage: null);
            Assert.True(capped.Trace!.IsComplete, "trace should be complete after a capped run");
            Assert.True(File.Exists(capped.Trace.FilePath));

            foreach (var refSig in full.Waveform.Signals)
            {
                var sig = capped.Waveform.Signals.Single(s => s.Name == refSig.Name);
                Assert.NotNull(sig.Archive);
                var archived = sig.AllTransitions().ToList();
                Assert.Equal(refSig.Transitions.Count, archived.Count);
                Assert.Equal(refSig.Transitions, archived);
            }
        }
        finally
        {
            capped.Trace?.Dispose();
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    // 3 — reading through the archive renders exactly like the in-memory path.
    [Fact]
    public async Task Archive_ViewQueries_MatchInMemory()
    {
        var storage = NewStorage(out var dir);
        var capped = await RunAsync(12000, historyCap: 5000, storage);
        try
        {
            var full = await RunAsync(12000, historyCap: 0, storage: null);
            long endNs = (long)full.Waveform.TotalTicks * TickResNs;
            var rnd = new Random(1234);
            foreach (var refSig in full.Waveform.Signals)
            {
                var sig = capped.Waveform.Signals.Single(s => s.Name == refSig.Name);
                for (int k = 0; k < 40; k++)
                {
                    long ns = (long)(rnd.NextDouble() * endNs);
                    Assert.Equal(refSig.ValueAtNs(ns), sig.ValueAtNs(ns));
                }
                // Windows that start before the in-memory window (archive path), at
                // several zoom levels incl. fully zoomed out (decimation).
                foreach (int width in new[] { 50, 400, 100000 })
                {
                    long a = 0, b = endNs;
                    AssertSameWindow(refSig.GetWindow(a, b, width), sig.GetWindow(a, b, width));
                    long mid = endNs / 3;
                    AssertSameWindow(refSig.GetWindow(mid, mid + endNs / 50, width),
                                     sig.GetWindow(mid, mid + endNs / 50, width));
                }
            }
        }
        finally
        {
            capped.Trace?.Dispose();
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    private static void AssertSameWindow(WaveformWindow expected, WaveformWindow actual)
    {
        Assert.Equal(expected.HaveEntry, actual.HaveEntry);
        Assert.Equal(expected.Entry, actual.Entry);
        Assert.Equal(expected.Points.ToList(), actual.Points.ToList());
    }

    // 4 — the streaming decimator matches GetWindow on arbitrary data.
    [Fact]
    public void ArchiveWindow_MatchesInMemoryWindow_OnRandomData()
    {
        var rnd = new Random(42);
        for (int trial = 0; trial < 30; trial++)
        {
            var list = new List<WaveformTransition>();
            long t = rnd.Next(0, 50);
            int n = rnd.Next(1, 3000);
            for (int i = 0; i < n; i++)
            {
                t += rnd.Next(1, 40);
                double? v = rnd.Next(10) == 0 ? null : rnd.Next(0, 16);
                list.Add(new WaveformTransition(t, v, v?.ToString()));
            }
            var mem = new WaveformSignal { Name = "s", Transitions = list };
            var arc = new WaveformSignal { Name = "s", Archive = new ListArchive(list) };
            foreach (int width in new[] { 0, 1, 7, 64, 500, 5000 })
            {
                long a = rnd.Next(-20, (int)Math.Min(int.MaxValue, t));
                long b = a + rnd.Next(1, (int)Math.Max(2, t));
                AssertSameWindow(mem.GetWindow(a, b, width), arc.GetWindow(a, b, width));
            }
        }
    }

    private sealed class ListArchive : ITransitionArchive
    {
        private readonly List<WaveformTransition> _l;
        public ListArchive(List<WaveformTransition> l) { _l = l; }
        public long Count => _l.Count;
        public WaveformTransition? LastAtOrBefore(long ns)
        {
            WaveformTransition? r = null;
            foreach (var x in _l) { if (x.TimeNs <= ns) r = x; else break; }
            return r;
        }
        public IEnumerable<WaveformTransition> ReadRange(long fromNs, long toNs)
            => _l.Where(x => x.TimeNs >= fromNs && x.TimeNs <= toNs);
    }

    // 5 — short runs never touch disk; dispose deletes the file.
    [Fact]
    public async Task ShortRun_CreatesNoFile_AndDisposeDeletes()
    {
        var storage = NewStorage(out var dir);
        try
        {
            var shortRun = await RunAsync(200, historyCap: 5000, storage);
            Assert.False(shortRun.Trace!.HasSpilled);
            Assert.False(shortRun.Trace.IsComplete);
            Assert.All(shortRun.Waveform.Signals, s => Assert.Null(s.Archive));
            Assert.Empty(Directory.EnumerateFiles(dir));

            var longRun = await RunAsync(12000, historyCap: 5000, storage);
            Assert.True(File.Exists(longRun.Trace!.FilePath));
            longRun.Trace.Dispose();
            Assert.False(File.Exists(longRun.Trace.FilePath));
            Assert.All(longRun.Waveform.Signals, s => Assert.Null(s.ValueAtNs(-1).Value));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }
}
