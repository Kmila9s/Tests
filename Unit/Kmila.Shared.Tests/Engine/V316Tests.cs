using Kmila.Shared.Services.Api;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.6: drivers and resolution (G43, G71), waveforms and delay mechanisms (G63),
/// <c>inout</c> (G53), implicit sensitivity (G62, G67, G72, G73), time and <c>now</c>
/// (G64, G78), arithmetic with width and sign (G44, G59, G65, G66, G68, G69),
/// identifiers (G74) and the remaining constructs (G54, G77, G79, G80, G81). The designs
/// and testbenches are the repros of <c>Documentacion/handoffs/repros_gaps_2026-11-16/v316/</c>
/// verbatim (<c>Fixtures/V316</c>); the expected values are GHDL 6.0.0's output.
/// </summary>
public class V316Tests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "V316", name));

    private static string[] Lines(string name) => Fixture(name).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Simulates the testbench <paramref name="entry"/> over <paramref name="files"/> (analysed in that order).</summary>
    private static Task<HeadlessRunResult> RunTestbenchAsync(string entry, int ns, params string[] files) =>
        RunFilesAsync(files.ToDictionary(f => f, Fixture), entry, ns, order: files);

    /// <summary>Asserts every sample GHDL printed for <paramref name="prefix"/>.<c>signal</c> (settled at each ns).</summary>
    private static void AssertSamples(HeadlessRunResult run, string expected, string prefix, string signals)
    {
        Assert.Null(run.EngineError);
        var samples = GhdlSamples(Lines(expected));
        foreach (var signal in signals.Split(' '))
            foreach (var (t, bits) in samples[$"{prefix}.{signal}"])
                Assert.True(bits == RawAt(run, signal, t), $"{prefix}.{signal} @ {t} ns: GHDL {bits}, Kmila {RawAt(run, signal, t)}");
    }

    /// <summary>G43: several drivers on resolved signals take the IEEE 1164 resolution, per driven element.</summary>
    [Fact]
    public async Task Drivers_resolve_like_GHDL()
    {
        var run = await RunTestbenchAsync("tb_v316b", 50, "drivers.vhdl", "after_w.vhdl", "tb_v316b.vhdl");
        AssertSamples(run, "tb_v316b.expected.txt", "dr", "y2 bus_t lh v w k");
    }

    /// <summary>G63: waveforms, inertial/transport/reject delays, and a matured transaction read at its own time.</summary>
    [Fact]
    public async Task Waveforms_and_delay_mechanisms_match_GHDL()
    {
        var run = await RunTestbenchAsync("tb_v316b", 50, "drivers.vhdl", "after_w.vhdl", "tb_v316b.vhdl");
        AssertSamples(run, "tb_v316b.expected.txt", "aw", "y_wave y_inert y_trans y_rej y_seq r");
    }

    /// <summary>G71: two drivers on an unresolved signal stop the run with GHDL's «several sources» error.</summary>
    [Theory]
    [InlineData("unres_sul", "0", "1")]
    [InlineData("unres_int", "1", "2")]
    [InlineData("unres_bit", "0", "1")]
    public async Task Unresolved_signal_with_two_drivers_is_an_error(string entry, string a, string b)
    {
        var run = await RunFilesAsync(new Dictionary<string, string> { ["unresolved.vhdl"] = Fixture("unresolved.vhdl") }, entry, 10,
            moments: [Port("a", At(0, a)), Port("b", At(0, b))]);
        Assert.NotNull(run.EngineError);
        Assert.Contains("several sources for unresolved signal", run.EngineError);
    }

    /// <summary>G53/G70: inout ports read the resolved bus and add their drivers to it (two cells, the testbench, an element, open).</summary>
    [Fact]
    public async Task Inout_ports_drive_and_read_the_bus()
    {
        var run = await RunTestbenchAsync("tb_inout", 60, "inout_bus.vhdl", "tb_inout.vhdl");
        AssertSamples(run, "tb_inout.expected.txt", "io", "pad q1 q2 bus4 q4");
    }

    /// <summary>G53: a design assigning its own input port is rejected, as GHDL does («port "a" can't be assigned»).</summary>
    [Fact]
    public async Task Assigning_an_input_port_is_an_error()
    {
        string? error;
        try
        {
            error = (await RunFilesAsync(new Dictionary<string, string> { ["in_write.vhdl"] = Fixture("in_write.vhdl") }, "in_write", 10,
                moments: [Port("a", At(0, "0"))])).EngineError;
        }
        catch (Exception e) { error = e.Message; }
        Assert.Contains("cannot assign to input port \"a\"", error);
    }
}
