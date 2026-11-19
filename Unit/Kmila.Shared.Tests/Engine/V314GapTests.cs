using Kmila.Shared.Models;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.4 gaps left by v3.1.3: aggregate assignment targets (G55), string and
/// character literals kept whole by the tokenizer (G56) and instances inside
/// generate bodies (G57). The designs are the repros of
/// <c>Documentacion/handoffs/repros_gaps_2026-11-16/v314/</c> verbatim
/// (<c>Fixtures/V314</c>); expected values are GHDL 6.0.0's output of
/// <c>tb_v314.vhdl</c> (<c>tb_v314.expected.txt</c>).
/// </summary>
public class V314GapTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "V314", name));

    private static string[] GhdlOutput => Fixture("tb_v314.expected.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>GHDL's samples: "prefix.output" → (time ns → bits).</summary>
    private static readonly Lazy<Dictionary<string, Dictionary<int, string>>> Expected = new(() => GhdlSamples(GhdlOutput));

    /// <summary>The testbench stimulus (tb_v314.vhdl): <c>n</c> drives agg_target's <c>v</c>.</summary>
    private static PortMomentConfiguration Drive(string port) => port switch
    {
        "n" or "v" => Port(port, At(0, "0101"), At(20, "1011"), At(40, "1110"), At(60, "0111")),
        "sel" => Port(port, At(0, "00"), At(20, "01"), At(40, "10"), At(60, "11")),
        _ => throw new ArgumentException(port),
    };

    /// <summary>file/top entity, GHDL prefix, outputs, inputs.</summary>
    public static TheoryData<string, string, string, string> Designs => new()
    {
        { "agg_target", "ag", "a b c d e f g h p q r s hi lo", "sel v" },
        { "strings", "st", "y", "n" },
        { "gen_inst", "gi", "y w z e", "n" },
    };

    [Theory]
    [MemberData(nameof(Designs))]
    public async Task Each_output_matches_GHDL(string entry, string prefix, string outputs, string inputs)
    {
        var wf = await SimulateAsync(Fixture($"{entry}.vhdl"), entry, inputs.Split(' ').Select(Drive).ToArray());
        AssertMatchesGhdl(wf, Expected.Value, entry, prefix, outputs);
    }

    /// <summary>G56: report and textio text is GHDL's, blanks and punctuation included.</summary>
    [Fact]
    public async Task Report_and_textio_text_is_GHDLs()
    {
        var run = await RunAsync(Fixture("strings.vhdl"), "strings", 20, Drive("n"));
        foreach (var line in GhdlOutput.Where(l => l.Contains("strings:")))
        {
            string text = line.Contains("(report note): ") ? line[(line.IndexOf("(report note): ") + 15)..] : line;
            Assert.Contains(run.Log, l => l.EndsWith(text.TrimEnd('\r'), StringComparison.Ordinal));
        }
    }
}
