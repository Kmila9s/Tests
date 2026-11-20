using Interpreter.Models;
using Kmila.Shared.Models;
using Kmila.Shared.Services;
using Kmila.Shared.Services.Api;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.5: IEEE 1164 nine-valued logic (G41), numeric_std metavalues, LRM initial values (G39),
/// edges, string/character (G58), integer mod/rem/abs (G47/G48) and run-time errors (G46, G49).
/// The designs are the repros of <c>Documentacion/handoffs/repros_gaps_2026-11-16/v315/</c>
/// verbatim (<c>Fixtures/V315</c>); expected values are GHDL 6.0.0's output of
/// <c>tb_logic9.vhdl</c>, <c>tb_v315.vhdl</c> and the error testbenches.
/// </summary>
public class V315LrmTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "V315", name));

    private static string[] Lines(string name) => Fixture(name).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static readonly Lazy<Dictionary<string, Dictionary<int, string>>> Logic9 = new(() => GhdlSamples(Lines("tb_logic9.expected.txt")));
    private static readonly Lazy<Dictionary<string, Dictionary<int, string>>> V315 = new(() => GhdlSamples(Lines("tb_v315.expected.txt")));

    private const string Nine = "UX01ZWLH-";

    /// <summary>The 9×9 tables of StdLogic are GHDL's (tb_logic9: one (a, b) pair per ns).</summary>
    [Fact]
    public void Truth_tables_are_IEEE_1164()
    {
        var s = Logic9.Value;
        foreach (var (k, a) in s["lg.a"])
        {
            char x = a[0], y = s["lg.b"][k][0];
            Assert.Equal(s["lg.y_and"][k][0], StdLogic.And(x, y));
            Assert.Equal(s["lg.y_or"][k][0], StdLogic.Or(x, y));
            Assert.Equal(s["lg.y_xor"][k][0], StdLogic.Xor(x, y));
            Assert.Equal(s["lg.y_nand"][k][0], StdLogic.Not(StdLogic.And(x, y)));
            Assert.Equal(s["lg.y_nor"][k][0], StdLogic.Not(StdLogic.Or(x, y)));
            Assert.Equal(s["lg.y_xnor"][k][0], StdLogic.Not(StdLogic.Xor(x, y)));
            Assert.Equal(s["lg.y_not"][k][0], StdLogic.Not(x));
            Assert.Equal(s["lg.m_eq"][k][0], StdLogic.MatchEq(x, y));
            Assert.Equal(s["lg.x01"][k][0], StdLogic.ToX01(x));
            Assert.Equal(s["lg.sm"][k] == "1", StdLogic.Match(x, y));
            Assert.Equal(s["lg.q1"][k] == "1", StdLogic.Condition(x));
        }
    }

    /// <summary>logic9 simulated: operators, reduction, ?=, ??, a = 'X', std_match, is_x, to_x01, to_01 (all 81 pairs).</summary>
    [Fact]
    public async Task Logic9_matches_GHDL()
    {
        var s = Logic9.Value;
        string[] vs = ["1111", "1101", "0000", "000U", "11X1", "HHHH", "LLHL", "1Z11", "-111"];
        var a = new List<PortMomentValue>(); var b = new List<PortMomentValue>(); var v = new List<PortMomentValue>();
        for (int k = 0; k < 81; k++)
        {
            a.Add(At(k, Nine[k / 9].ToString())); b.Add(At(k, Nine[k % 9].ToString())); v.Add(At(k, vs[k % 9]));
        }
        var run = await RunAsync(Fixture("logic9.vhdl"), "logic9", 82, Port("a", [.. a]), Port("b", [.. b]), Port("v", [.. v]));
        foreach (var port in "y_and y_or y_xor y_nand y_nor y_xnor y_not r_and r_or r_xor m_eq m_ne q1 q2 eqx eqa sm isx x01 v01 vand".Split(' '))
            foreach (var (k, bits) in s[$"lg.{port}"])
                Assert.True(bits == RawAt(run, port, k), $"logic9.{port} @ {k} ns: GHDL {bits}, Kmila {RawAt(run, port, k)}");
    }

    /// <summary>tb_v315's stimulus for each input.</summary>
    private static PortMomentConfiguration Drive(string port) => port switch
    {
        "x" => Port(port, At(0, "0011"), At(20, "00U1"), At(40, "HL01"), At(60, "0X10")),
        "y" => Port(port, At(0, "0001"), At(20, "0001"), At(40, "0010"), At(60, "ZZZZ")),
        "sel" => Port(port, At(0, "00"), At(20, "01"), At(40, "10"), At(60, "11")),
        "n" => Port(port, At(0, "1"), At(20, "0"), At(40, "1"), At(60, "0")),
        "clk" => Port(port, [.. Enumerable.Range(0, 16).Select(i => At(i * 5, i % 2 == 0 ? "1" : "0"))]),
        "d" => Port(port, At(2, "1"), At(22, "0"), At(42, "1"), At(62, "0")),
        "c" => Port(port, At(1, "1"), At(3, "0"), At(5, "H"), At(21, "X"), At(23, "1"), At(25, "L"),
            At(41, "1"), At(43, "W"), At(45, "0"), At(61, "Z"), At(63, "1"), At(65, "0")),
        _ => throw new ArgumentException(port),
    };

    /// <summary>file/top entity, GHDL prefix, outputs, inputs.</summary>
    public static TheoryData<string, string, string, string> Designs => new()
    {
        { "numeric_meta", "nm", "s_add s_sub s_mul lt eq ti rs sr", "x y" },
        { "arith_int", "ai", "y_mod y_rem y_div y_abs", "sel" },
        { "init_lrm", "il", "q s0 pv pd en bo bt cnt vi iv ir nt", "clk d" },
        { "edges", "ed", "n_rise n_fall n_ev", "c" },
        { "strings_g58", "sg", "ceq seq cat len3 lex", "n" },
    };

    [Theory]
    [MemberData(nameof(Designs))]
    public async Task Each_output_matches_GHDL(string entry, string prefix, string outputs, string inputs)
    {
        var run = await RunFilesAsync(new Dictionary<string, string> { [$"{entry}.vhdl"] = Fixture($"{entry}.vhdl") }, entry, 80,
            moments: [.. inputs.Split(' ').Select(Drive)]);
        Assert.Null(run.EngineError);
        foreach (var port in outputs.Split(' '))
            foreach (var (t, bits) in V315.Value[$"{prefix}.{port}"])
                Assert.True(bits == RawAt(run, port, t), $"{entry}.{port} @ {t} ns: GHDL {bits}, Kmila {RawAt(run, port, t)}");
    }

    /// <summary>
    /// numeric_std warnings carry GHDL's text; arithmetic on metavalues does not warn. The
    /// <c>"="</c> warning is left out: Kmila does not tell <c>unsigned</c> from
    /// <c>std_logic_vector</c> yet (G59), so equality of two vectors is the predefined one.
    /// </summary>
    [Fact]
    public async Task Numeric_std_warnings_are_GHDLs()
    {
        var run = await RunAsync(Fixture("numeric_meta.vhdl"), "numeric_meta", 30, Drive("x"), Drive("y"));
        foreach (var line in Lines("tb_v315.expected.txt").Where(l => l.Contains("NUMERIC_STD.") && !l.Contains("NUMERIC_STD.\"=\"")).Select(l => l[(l.IndexOf("NUMERIC_STD.", StringComparison.Ordinal))..].TrimEnd('\r')).Distinct())
            Assert.Contains(run.Log, l => l.EndsWith(line, StringComparison.Ordinal));
        Assert.DoesNotContain(run.Log, l => l.Contains("NUMERIC_STD.\"+\""));
    }

    /// <summary>G58: report with string objects prints GHDL's text.</summary>
    [Fact]
    public async Task String_report_is_GHDLs()
    {
        var run = await RunAsync(Fixture("strings_g58.vhdl"), "strings_g58", 30, Drive("n"));
        foreach (var text in Lines("tb_v315.expected.txt").Where(l => l.Contains("g58: ")).Select(l => l[(l.IndexOf("g58: ", StringComparison.Ordinal))..].TrimEnd('\r')).Distinct())
            Assert.Contains(run.Log, l => l.EndsWith(text, StringComparison.Ordinal));
    }

    /// <summary>G46/G49: run-time errors stop the run with GHDL's message and the statement's line.</summary>
    [Theory]
    [InlineData("div0", "z", "0011", "0000", "division by 0")]
    [InlineData("idx_rd", "i", "010", "110", "index (6) out of bounds (3 downto 0) at Line 19")]
    [InlineData("idx_wr", "i", "010", "110", "index (6) out of bounds (3 downto 0) at Line 20")]
    public async Task Run_time_errors_stop_the_run(string entry, string input, string ok, string bad, string message)
    {
        var run = await RunAllowingEngineErrorAsync(Fixture($"{entry}.vhdl"), entry, 30, Port(input, At(0, ok), At(10, bad)));
        Assert.NotNull(run.EngineError);
        Assert.Contains(message, run.EngineError);
    }

    /// <summary>The didactic policy starts objects without := at 0 (a counter without reset counts) and drives the clk port.</summary>
    [Fact]
    public async Task Didactic_policy_seeds_zero_and_proposes_the_clock()
    {
        var files = new Dictionary<string, string> { ["init_lrm.vhdl"] = Fixture("init_lrm.vhdl") };
        var lrm = await RunFilesAsync(files, "init_lrm", 30, moments: [Drive("d")]);
        var didactic = await RunFilesAsync(files, "init_lrm", 30, stimulus: StimulusOptions.Declared(InitialValuePolicy.Didactic, null, null), moments: [Drive("d")]);
        // Both runs drive the clock-named port (proposed by name); only the start differs.
        Assert.Equal("XXXX", RawAt(lrm, "cnt", 29));
        Assert.Equal("U", RawAt(lrm, "s0", 29));
        Assert.Equal("0001", RawAt(didactic, "cnt", 29));
        Assert.Equal("0", RawAt(didactic, "s0", 29));
        Assert.DoesNotContain(lrm.History.GetTrackedSignals(), v => v.Name == "_clk");
    }
}
