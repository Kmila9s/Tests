using Kmila.Shared.Models;
using Kmila.Shared.Services;
using Kmila.Shared.Services.Api;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// Runs a design through the headless simulator the way the app does and
/// reads its waveform: the shared harness of the GHDL-compared engine tests.
/// </summary>
internal static class EngineHarness
{
    /// <summary>A stimulus value at <paramref name="ns"/> nanoseconds.</summary>
    public static PortMomentValue At(int ns, string v) => new() { Time = ns, TimeScale = "NS", Value = v };

    /// <summary>The stimulus of one input (a port, or a part of one such as <c>p.lo</c>).</summary>
    public static PortMomentConfiguration Port(string name, params PortMomentValue[] values) =>
        new() { PortName = name, Values = [.. values] };

    /// <summary>
    /// Simulates <paramref name="vhdl"/> (one file, top <paramref name="entry"/>)
    /// for <paramref name="totalNs"/> ns at 1 ns resolution (50 MHz clock) and
    /// returns the run; fails when the engine reports an error.
    /// </summary>
    public static async Task<HeadlessRunResult> RunAsync(string vhdl, string entry, int totalNs, params PortMomentConfiguration[] moments)
    {
        var run = await RunAllowingEngineErrorAsync(vhdl, entry, totalNs, moments);
        Assert.Null(run.EngineError);
        return run;
    }

    /// <summary><see cref="RunAsync"/> for a design expected to fail at run time (<c>severity failure</c>).</summary>
    public static Task<HeadlessRunResult> RunAllowingEngineErrorAsync(string vhdl, string entry, int totalNs, params PortMomentConfiguration[] moments) =>
        RunFilesAsync(new Dictionary<string, string> { [$"{entry}.vhdl"] = vhdl }, entry, totalNs, moments: moments);

    /// <summary>
    /// Simulates a multi-file project: <paramref name="files"/> (relative path →
    /// text) written to a sandbox, analysed in <paramref name="order"/> when given
    /// (else sorted by path), top unit <paramref name="entry"/>.
    /// </summary>
    public static async Task<HeadlessRunResult> RunFilesAsync(IReadOnlyDictionary<string, string> files, string entry,
        int totalNs, IReadOnlyList<string>? order = null, bool entryIsExplicit = true, StimulusOptions? stimulus = null,
        params PortMomentConfiguration[] moments)
    {
        var store = new SandboxProjectStore();
        var project = store.Create("engine-harness", retain: false);
        try
        {
            foreach (var (name, text) in files) project.WriteFile(name, text);
            return await HeadlessSimulator.RunProjectAsync(project.Path, new HeadlessRunOptions
            {
                EntryPoint = entry,
                EntryIsExplicit = entryIsExplicit,
                Files = order,
                TotalNs = totalNs,
                TickResNs = 1,
                SystemClockMhz = 50,
                PortMoments = [.. moments],
                Stimulus = stimulus,
            });
        }
        finally { store.Delete(project.Id); }
    }

    /// <summary><see cref="RunAsync"/> for 100 ns, as a waveform.</summary>
    public static async Task<WaveformData> SimulateAsync(string vhdl, string entry, params PortMomentConfiguration[] moments)
    {
        var run = await RunAsync(vhdl, entry, 100, moments);
        return WaveformData.FromSignalHistory(run.History, run.TickFs);
    }

    /// <summary>
    /// The samples a GHDL testbench printed as <c>report "t=N p.o=bits …"</c>:
    /// "prefix.output" → (time ns → bits).
    /// </summary>
    public static Dictionary<string, Dictionary<int, string>> GhdlSamples(IEnumerable<string> ghdlOutput)
    {
        var result = new Dictionary<string, Dictionary<int, string>>();
        foreach (var line in ghdlOutput.Where(l => l.Contains("): t=")))
        {
            var parts = line[(line.IndexOf("): t=") + 3)..].Trim().Split(' ');
            int t = int.Parse(parts[0][2..]);
            foreach (var kv in parts.Skip(1).Select(p => p.Split('=')))
            {
                if (!result.TryGetValue(kv[0], out var series)) result[kv[0]] = series = [];
                series[t] = kv[1];
            }
        }
        return result;
    }

    /// <summary>Asserts every output of <paramref name="outputs"/> matches GHDL's samples <c>prefix.output</c>.</summary>
    public static void AssertMatchesGhdl(WaveformData wf, Dictionary<string, Dictionary<int, string>> samples,
        string entry, string prefix, string outputs)
    {
        foreach (var port in outputs.Split(' '))
            foreach (var (t, bits) in samples[$"{prefix}.{port}"])
                Assert.True(Convert.ToInt64(bits, 2) == ValueAt(wf, port, t), $"{entry}.{port} @ {t} ns: GHDL {bits}, Kmila {ValueAt(wf, port, t)}");
    }

    /// <summary>
    /// The value <paramref name="signal"/> holds at <paramref name="ns"/> after its deltas settled, as
    /// written (metavalues kept, upper case): <c>U</c>, <c>0X10</c>, <c>-4</c>… (1 ns ticks).
    /// </summary>
    public static string? RawAt(HeadlessRunResult run, string signal, long ns)
    {
        var v = run.History.GetTrackedSignals().Single(s => s.Name.Equals(signal, StringComparison.OrdinalIgnoreCase));
        long tick = (long)WaveformData.TickAt(ns, run.TickFs);
        return run.History.GetValueAtTime(v, Interpreter.Services.SignalHistory.SettledMoment(tick))?.ToString()?.ToUpperInvariant();
    }

    /// <summary>Value of <paramref name="signal"/> at <paramref name="ns"/> (bits as a number).</summary>
    public static double? ValueAt(WaveformData wf, string signal, long ns) =>
        wf.Signals.Single(s => s.Name.Equals(signal, StringComparison.OrdinalIgnoreCase)).ValueAtNs(ns).Value;
}
