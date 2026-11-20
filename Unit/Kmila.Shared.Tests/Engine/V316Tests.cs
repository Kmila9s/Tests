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

    /// <summary>
    /// G62/G67/G72/G73: implicit sensitivity counts only the signals named in the statement (not
    /// those read inside a called function), for simple, conditional and selected assignments,
    /// process (all) and a concurrent procedure call; each instance has its own subprograms.
    /// </summary>
    [Fact]
    public async Task Implicit_sensitivity_is_the_statement_text()
    {
        var run = await RunTestbenchAsync("tb_sens", 40, "sens.vhdl", "tb_sens.vhdl");
        AssertSamples(run, "tb_sens.expected.txt", "se", "o1 o2 oc os oa op pc_out");
    }

    /// <summary>G72: a process with neither a sensitivity list nor a wait never suspends: an error with its line, not a hang.</summary>
    [Fact]
    public async Task Process_without_sensitivity_or_wait_is_an_error()
    {
        var run = await RunAllowingEngineErrorAsync(Fixture("nowait.vhdl"), "nowait", 10, Port("a", At(0, "0")));
        Assert.NotNull(run.EngineError);
        Assert.Contains("never suspends", run.EngineError);
        Assert.Contains("Line 11", run.EngineError);
    }

    /// <summary>
    /// G44/G59/G65/G68/G69: signed and unsigned arithmetic of any width, numeric_std result widths,
    /// std_logic_signed/unsigned/arith, to_integer selecting a ROM entry and a case choice, and
    /// <c>&amp;</c> inside a function argument (tb_v316f: entities that print integers print them as bits).
    /// </summary>
    [Theory]
    [InlineData("ns", "rz8 rz2 sr sl ab ng lt gt0 eqw mi dv md rm")]
    [InlineData("ar", "sx ex")]
    [InlineData("wd", "s40 p80 lt40 s64 s96 neg64")]
    [InlineData("wi", "y8 y9 yi")]
    [InlineData("lt10", "d c")]
    [InlineData("ca", "y z")]
    public async Task Numeric_operators_match_GHDL(string prefix, string signals)
    {
        var run = await RunTestbenchAsync("tb_v316f", 40, Numeric);
        var names = new Dictionary<string, string>
        {
            ["ar.sx"] = "ssx", ["ar.ex"] = "sex", ["lt10.d"] = "dd", ["lt10.c"] = "cc", ["ca.y"] = "cy", ["ca.z"] = "cz",
        };
        Assert.Null(run.EngineError);
        var samples = GhdlSamples(Lines("tb_v316f.expected.txt"));
        foreach (var signal in signals.Split(' '))
        {
            string key = $"{prefix}.{signal}";
            string name = names.GetValueOrDefault(key, signal);
            foreach (var (t, bits) in samples[key])
                Assert.True(bits == RawAt(run, name, t), $"{key} @ {t} ns: GHDL {bits}, Kmila {RawAt(run, name, t)}");
        }
    }

    /// <summary>G59: the integers of tb_v316f (to_integer of signed, conv_integer under std_logic_signed/unsigned, a 31-bit to_integer).</summary>
    [Theory]
    [InlineData("ns.ti", "ti", true)]
    [InlineData("sg.i", "si", true)]
    [InlineData("us.i", "ui", true)]
    [InlineData("wd.i31", "i31", false)]
    [InlineData("sg.s", "ss", false)]
    [InlineData("us.s", "us", false)]
    [InlineData("sg.lt", "slt", false)]
    [InlineData("us.lt", "ult", false)]
    public async Task Integer_conversions_match_GHDL(string key, string signal, bool signedBits)
    {
        var run = await RunTestbenchAsync("tb_v316f", 40, Numeric);
        Assert.Null(run.EngineError);
        foreach (var (t, bits) in GhdlSamples(Lines("tb_v316f.expected.txt"))[key])
        {
            string? kmila = RawAt(run, signal, t);
            string expected = bits.Length > 1 && (signedBits || signal == "i31")
                ? (signedBits ? (Convert.ToInt64(bits, 2) - (bits[0] == '1' ? 1L << bits.Length : 0)) : Convert.ToInt64(bits, 2)).ToString()
                : bits;
            Assert.True(expected == kmila, $"{key} @ {t} ns: GHDL {bits} ({expected}), Kmila {kmila}");
        }
    }

    private static readonly string[] Numeric =
        ["numeric_signed.vhdl", "slv_pkgs.vhdl", "wide.vhdl", "widths.vhdl", "lit10.vhdl", "concat_arg.vhdl", "tb_v316f.vhdl"];

    /// <summary>
    /// G66/G68: a subtype range violation and a length mismatch stop the run with GHDL's
    /// «bound check failure» at the statement's line (the entities simulated directly, as GHDL's
    /// testbenches drive them: <c>go</c> rises at 10 ns).
    /// </summary>
    [Theory]
    [InlineData("nat_neg", "int_range.vhdl", "go", "1", "Line 34")]
    [InlineData("sub_rng", "int_range.vhdl", "go", "1", "Line 53")]
    [InlineData("len_err", "widths.vhdl", "a", "0010", "Line 31")]
    public async Task Bound_check_failures_stop_the_run(string entity, string design, string input, string value, string line)
    {
        var run = await RunFilesAsync(new Dictionary<string, string> { [design] = Fixture(design) }, entity, 30,
            moments: [Port(input, At(0, input == "go" ? "0" : "0001"), At(10, value))]);
        Assert.NotNull(run.EngineError);
        Assert.True(run.EngineError.Contains("bound check failure") && run.EngineError.Contains(line), run.EngineError);
    }

    /// <summary>G66: the base type integer wraps at 32 bits without an error (GHDL does not check it).</summary>
    [Fact]
    public async Task Integer_overflow_wraps_like_GHDL()
    {
        var run = await RunFilesAsync(new Dictionary<string, string> { ["int_range.vhdl"] = Fixture("int_range.vhdl"), ["tb_v316err.vhdl"] = Fixture("tb_v316err.vhdl") },
            "tb_ovf", 30, order: ["int_range.vhdl", "tb_v316err.vhdl"]);
        Assert.Null(run.EngineError);
        Assert.Equal("2147483647", RawAt(run, "y", 5));
        Assert.Equal("-2147483648", RawAt(run, "y", 15));
    }
}
