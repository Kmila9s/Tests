using Kmila.Shared.Models;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.4 elaboration (G18–G24): architecture selection (explicit <c>(arch)</c>,
/// last analysed by default), configuration specifications and declarations,
/// <c>component</c>/<c>configuration</c> instances, entity and architecture in
/// different files, analysis order and the top-unit check. The designs are the
/// repros of <c>Documentacion/handoffs/repros_gaps_2026-11-16/elab/</c> and
/// <c>v314/</c> verbatim (<c>Fixtures/Elaboration</c>); expected values are
/// GHDL 6.0.0's (<c>v314/EXPECTED.md</c>).
/// </summary>
public class ElaborationTests
{
    private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Elaboration");

    /// <summary>A fixture file, or every file of a fixture folder, keyed by relative path.</summary>
    private static Dictionary<string, string> Files(string fixture)
    {
        string path = Path.Combine(FixtureDir, fixture);
        return Directory.Exists(path)
            ? Directory.GetFiles(path).ToDictionary(Path.GetFileName, File.ReadAllText)!
            : new() { [fixture] = File.ReadAllText(path) };
    }

    private static async Task<WaveformData> SimulateAsync(string fixture, string entry, IReadOnlyList<string>? order = null,
        params PortMomentConfiguration[] moments)
    {
        var run = await RunFilesAsync(Files(fixture), entry, 10, order, true, moments: moments);
        Assert.Null(run.EngineError);
        return WaveformData.FromSignalHistory(run.History, run.TickFs);
    }

    /// <summary>fixture, top unit, expected "port=bits" pairs at 5 ns (GHDL).</summary>
    public static TheoryData<string, string, string> Designs => new()
    {
        { "r1.vhd", "top", "oa=1010 ob=0101" },                         // G18 explicit (arch_b)
        { "r2.vhd", "top", "oa=1010" },                                 // G19 for u1 : foo use …(arch_a)
        { "r2b.vhd", "top", "oa=0101" },
        { "r3.vhd", "top", "o=0101" },                                  // G18 top: last analysed
        { "r4.vhd", "top", "oa=0101 ob=0101" },                         // G18 default binding: last analysed
        { "r5.vhd", "top", "o=0110" },                                  // G18 top whose first arch has an instance
        { "cfg_spec_all.vhd", "cfg_spec_all", "oa=1010 ob=1010" },      // G19 for all
        { "cfg_spec_others.vhd", "cfg_spec_others", "oa=0101 ob=1010 oc=1010" },   // G19 label + others
        { "cfg_decl.vhd", "cfg_decl", "o1=0101 o2=0101" },              // G20 no configuration: default binding
        { "cfg_decl.vhd", "cfg_top", "o1=1010 o2=0101" },               // G20 configuration as the top
        { "cfg_inst.vhd", "cfg_inst", "oa=1010 ob=0101" },              // G20 u1: configuration work.cfg_a
        { "comp_kw.vhd", "comp_kw", "y=1001" },                         // G24 component c generic map … port map
        { "order", "order_top", "o=0101" },                             // G22 sorted by path: b_foo_b.vhd last
    };

    [Theory]
    [MemberData(nameof(Designs))]
    public async Task Each_output_matches_GHDL(string fixture, string entry, string expected)
    {
        var wf = await SimulateAsync(fixture, entry);
        foreach (var (port, bits) in expected.Split(' ').Select(p => p.Split('=')).Select(p => (p[0], p[1])))
            Assert.True(Convert.ToInt64(bits, 2) == ValueAt(wf, port, 5), $"{fixture}/{entry}.{port}: GHDL {bits}, Kmila {ValueAt(wf, port, 5)}");
    }

    /// <summary>G19: a configuration specification declares no object (it used to become a 32-bit signal).</summary>
    [Fact]
    public async Task A_configuration_specification_is_not_a_signal()
    {
        var wf = await SimulateAsync("r2.vhd", "top");
        Assert.DoesNotContain(wf.Signals, s => s.Name.Equals("u1", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>G24: the component instance drives its output (o = not a).</summary>
    [Fact]
    public async Task A_component_keyword_instance_is_elaborated()
    {
        var wf = await SimulateAsync("comp_kw.vhd", "comp_kw", null, Port("a", At(0, "1")));
        Assert.Equal(0, ValueAt(wf, "o", 5));
    }

    /// <summary>G21: entity and architecture in different files, with and without instances.</summary>
    [Theory]
    [InlineData("split_arch", "split_top", "1100")]     // o = not n
    [InlineData("split_plain", "split_plain", "1001")]  // o = n(0) & n(3 downto 1)
    public async Task An_architecture_in_another_file_is_found(string fixture, string entry, string o)
    {
        var wf = await SimulateAsync(fixture, entry, null, Port("n", At(0, "0011")));
        Assert.Equal(Convert.ToInt64(o, 2), ValueAt(wf, "o", 5));
    }

    /// <summary>G22: the order the caller gives is the analysis order (here a_foo.vhd's arch_a is analysed last).</summary>
    [Fact]
    public async Task The_given_file_order_is_the_analysis_order()
    {
        var wf = await SimulateAsync("order", "order_top", ["c_top.vhd", "b_foo_b.vhd", "a_foo.vhd"]);
        Assert.Equal(0b1010, ValueAt(wf, "o", 5));
    }

    /// <summary>G23: an explicit top unit that does not exist is an error, as in GHDL.</summary>
    [Fact]
    public async Task A_missing_explicit_top_unit_is_an_error()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => RunFilesAsync(Files("r3.vhd"), "nope", 10));
        Assert.Contains("cannot find entity or configuration \"nope\"", ex.Message);
    }

    /// <summary>G23: an inferred top unit that does not exist falls back to the first entity, with a warning.</summary>
    [Fact]
    public async Task A_missing_inferred_top_unit_falls_back_with_a_warning()
    {
        var run = await RunFilesAsync(Files("r3.vhd"), "r3", 10, entryIsExplicit: false);
        Assert.Equal("top", run.Design.Name);
        Assert.Contains(run.Log, l => l.Contains("cannot find entity or configuration \"r3\""));
    }

    /// <summary>G18: an explicit architecture that does not exist is an error (GHDL: architecture not found).</summary>
    [Fact]
    public async Task A_missing_explicit_architecture_is_an_error()
    {
        var files = Files("r1.vhd");
        files["r1.vhd"] = files["r1.vhd"].Replace("work.foo(arch_b)", "work.foo(arch_z)");
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => RunFilesAsync(files, "top", 10));
        Assert.Contains("architecture \"arch_z\" of \"foo\" not found", ex.Message);
    }
}
