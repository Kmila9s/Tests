using System.Text.RegularExpressions;
using Kmila.Shared.Models;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.3 (G27–G32): statement labels, exit/next with labels (sensitivity-list and
/// wait processes), concurrent assert and procedure calls, generate bodies,
/// VHDL-2008 sequential conditional/selected assignments, std.env.stop/finish,
/// severity failure and textio output. The designs are the repros of
/// <c>Documentacion/handoffs/repros_gaps_2026-11-16/v313/</c> verbatim
/// (<c>Fixtures/Sequential</c>); expected values are GHDL 6.0.0's output of
/// <c>tb_v313.vhdl</c> (<c>tb_v313.expected.txt</c>) and its README.
/// </summary>
public class SequentialSemanticsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sequential", name));

    private static string[] GhdlOutput => Fixture("tb_v313.expected.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>GHDL's samples: "prefix.output" → (time ns → bits).</summary>
    private static readonly Lazy<Dictionary<string, Dictionary<int, string>>> Expected = new(() =>
    {
        var result = new Dictionary<string, Dictionary<int, string>>();
        foreach (var line in GhdlOutput.Where(l => l.Contains("): t=")))
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
    });

    /// <summary>The testbench stimulus every design receives (tb_v313.vhdl).</summary>
    private static PortMomentConfiguration Drive(string port) => port switch
    {
        "a" => Port(port, At(0, "1"), At(20, "0")),
        "b" => Port(port, At(0, "0"), At(20, "1")),
        "n" => Port(port, At(0, "0101"), At(20, "1011"), At(40, "1110"), At(60, "0111")),
        "sel" => Port(port, At(0, "00"), At(20, "01"), At(40, "10"), At(60, "11")),
        _ => throw new ArgumentException(port),
    };

    /// <summary>file/top entity, GHDL prefix, outputs, inputs.</summary>
    public static TheoryData<string, string, string, string> Designs => new()
    {
        { "labels", "lb", "y", "a b n" },
        { "exit_next", "en", "y z w u", "n" },
        { "conc_assert", "ca", "y z", "a b sel" },
        { "conc_proc", "cp", "y z", "a b" },
        // gen_body.y comes from an entity instance inside the generate: instances in
        // generate bodies share one inlined copy (G57, elaboration — v3.1.4).
        { "gen_body", "gb", "z", "n" },
        { "seq2008", "s8", "y z w", "a b sel" },
    };

    [Theory]
    [MemberData(nameof(Designs))]
    public async Task Each_output_matches_GHDL(string entry, string prefix, string outputs, string inputs)
    {
        var wf = await SimulateAsync(Fixture($"{entry}.vhdl"), entry, inputs.Split(' ').Select(Drive).ToArray());
        foreach (var port in outputs.Split(' '))
            foreach (var (t, bits) in Expected.Value[$"{prefix}.{port}"])
                Assert.True(Convert.ToInt64(bits, 2) == ValueAt(wf, port, t), $"{entry}.{port} @ {t} ns: GHDL {bits}, Kmila {ValueAt(wf, port, t)}");
    }

    /// <summary>Report/textio text compared without blanks (string literals are tokenized in pieces).</summary>
    private static string Squeeze(string s) => Regex.Replace(s, @"\s+", "");

    [Fact]
    public async Task Concurrent_asserts_report_like_GHDL()
    {
        var run = await RunAsync(Fixture("conc_assert.vhdl"), "conc_assert", 70, Drive("a"), Drive("b"), Drive("sel"));
        var log = Squeeze(string.Join('\n', run.Log));
        Assert.Contains(Squeeze("conc_assert: a is low"), log);
        Assert.Contains(Squeeze("conc_assert: b is high"), log);
    }

    [Fact]
    public async Task Textio_writeline_prints_each_line_like_GHDL()
    {
        var run = await RunAsync(Fixture("textio_out.vhdl"), "textio_out", 70, Drive("a"), Drive("n"));
        var log = run.Log.Select(Squeeze).ToList();
        foreach (var expected in GhdlOutput.Where(l => l.StartsWith("textio_out:") && !l.Contains("n=0 ")))
            Assert.Contains(log, l => l.Contains(Squeeze(expected)));
    }

    [Theory]
    [InlineData("env_stop", 0, "0100")]    // stop on the 5th rising edge (Kmila's 1 ns clock: 45 ns)
    [InlineData("env_finish", 30, "0010")] // GHDL: simulation finished @30ns
    public async Task Stop_and_finish_end_the_run_early_with_success(string entry, int endNs, string q)
    {
        var run = await RunAsync(Fixture($"{entry}.vhdl"), entry, 200);
        var wf = WaveformData.FromSignalHistory(run.History, 1);
        if (endNs > 0) Assert.Equal((ulong)endNs, wf.TotalTicks - 1);
        else Assert.True(wf.TotalTicks < 200, $"run did not stop early (ticks {wf.TotalTicks})");
        Assert.Equal(Convert.ToInt64(q, 2), ValueAt(wf, "q", (long)wf.TotalTicks - 1));
    }

    [Fact]
    public async Task Severity_failure_ends_the_run_with_an_error()
    {
        var run = await RunAllowingEngineErrorAsync(Fixture("env_fail.vhdl"), "env_fail", 200);
        Assert.NotNull(run.EngineError);
        Assert.Contains(Squeeze("env_fail: c reached 3"), Squeeze(run.EngineError!));
        var wf = WaveformData.FromSignalHistory(run.History, 1);
        Assert.Equal(40UL, wf.TotalTicks - 1);   // GHDL: assertion failure @40ns
        Assert.Equal(0b0011, ValueAt(wf, "q", 40));
    }
}
