using Kmila.Shared.Models;
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
    public static async Task<HeadlessRunResult> RunAllowingEngineErrorAsync(string vhdl, string entry, int totalNs, params PortMomentConfiguration[] moments)
    {
        var store = new SandboxProjectStore();
        var project = store.Create("engine-harness", retain: false);
        try
        {
            project.WriteFile($"{entry}.vhdl", vhdl);
            var run = await HeadlessSimulator.RunProjectAsync(project.Path, new HeadlessRunOptions
            {
                EntryPoint = entry,
                TotalNs = totalNs,
                TickResNs = 1,
                SystemClockMhz = 50,
                PortMoments = [.. moments],
            });
            return run;
        }
        finally { store.Delete(project.Id); }
    }

    /// <summary><see cref="RunAsync"/> for 100 ns, as a waveform.</summary>
    public static async Task<WaveformData> SimulateAsync(string vhdl, string entry, params PortMomentConfiguration[] moments) =>
        WaveformData.FromSignalHistory((await RunAsync(vhdl, entry, 100, moments)).History, 1);

    /// <summary>Value of <paramref name="signal"/> at <paramref name="ns"/> (bits as a number).</summary>
    public static double? ValueAt(WaveformData wf, string signal, long ns) =>
        wf.Signals.Single(s => s.Name.Equals(signal, StringComparison.OrdinalIgnoreCase)).ValueAtNs(ns).Value;
}
