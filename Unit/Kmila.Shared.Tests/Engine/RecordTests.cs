using Kmila.Shared.Models;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.2 (G01, G09–G16): records end to end — field reads and writes, nested
/// records, integer/enum/boolean fields, aggregates, package types and record
/// ports, arrays of records, ascending arrays, instances with record ports and
/// partial association, selected names, field stimulus and the per-field VCD.
/// The designs are the repros of
/// <c>Documentacion/handoffs/repros_gaps_2026-11-16/v312/</c> and <c>rec/</c>
/// verbatim (<c>Fixtures/Records</c>); expected values are GHDL 6.0.0's output
/// of <c>tb_v312.vhdl</c> (<c>tb_v312.expected.txt</c>).
/// </summary>
public class RecordTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Records", name));

    /// <summary>GHDL's samples: "prefix.output" → (time ns → bits).</summary>
    private static readonly Lazy<Dictionary<string, Dictionary<int, string>>> Expected = new(() =>
    {
        var result = new Dictionary<string, Dictionary<int, string>>();
        foreach (var line in Fixture("tb_v312.expected.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(' ');
            int t = int.Parse(parts[0][2..]);
            foreach (var kv in parts.Skip(1).Select(p => p.Split('=')))
            {
                if (!result.TryGetValue(kv[0], out var series)) result[kv[0]] = series = [];
                series[t] = kv[1];
            }
        }
        return result;
    });

    /// <summary>The testbench stimulus every design receives (tb_v312.vhdl).</summary>
    private static PortMomentConfiguration Drive(string port, string source) => source switch
    {
        "a" => Port(port, At(0, "1"), At(20, "0")),
        "b" => Port(port, At(0, "0"), At(20, "1")),
        "n" => Port(port, At(0, "0101"), At(20, "1010")),
        "sel" => Port(port, At(0, "00"), At(20, "01"), At(40, "10"), At(60, "11")),
        _ => throw new ArgumentException(source),
    };

    /// <summary>file, top entity, GHDL prefix, outputs (port=key), inputs (port=source).</summary>
    public static TheoryData<string, string, string, string, string> Designs => new()
    {
        { "rec_int_enum.vhdl", "rec_int_enum", "ie", "y z st f", "a=a n=n" },
        { "rec_nested.vhdl", "rec_nested", "nest", "o", "x=a w=b" },
        { "rec_vecconst.vhdl", "rec_vecconst", "vc", "y", "a=a din=n" },
        { "rec_var.vhdl", "rec_var", "rv", "y z u", "a=a b=b" },
        { "rec_agg_proc.vhdl", "rec_agg_proc", "ag", "y eq", "a=a b=b" },
        { "arr_rec.vhdl", "arr_rec", "ar", "y", "a=a b=b sel=sel" },
        { "arr_asc.vhdl", "arr_asc", "as", "y z", "a=a sel=sel" },
        { "rec_pkg.vhdl", "rec_pkg_top", "pk", "en_o=en on_o=on y d_o=d s", "a=a din=n" },
        { "inst_rec.vhdl", "inst_rec", "in", "y1 z1 y2 z2", "a=a b=b" },
        { "qualified.vhdl", "qualified", "q", "y", "x=n" },
    };

    [Theory]
    [MemberData(nameof(Designs))]
    public async Task Each_output_matches_GHDL(string file, string entry, string prefix, string outputs, string inputs)
    {
        var moments = inputs.Split(' ').Select(i => i.Split('=')).Select(i => Drive(i[0], i[1])).ToArray();
        var wf = await SimulateAsync(Fixture(file), entry, moments);
        foreach (var output in outputs.Split(' '))
        {
            var (port, key) = output.Contains('=') ? (output.Split('=')[0], output.Split('=')[1]) : (output, output);
            foreach (var (t, bits) in Expected.Value[$"{prefix}.{key}"])
                Assert.True(Convert.ToInt64(bits, 2) == ValueAt(wf, port, t), $"{entry}.{port} @ {t} ns: GHDL {bits}, Kmila {ValueAt(wf, port, t)}");
        }
    }

    /// <summary>
    /// The first record repros (<c>rec/</c>): y and z follow a and b through
    /// fields written concurrently, in a process, from an identifier list, by
    /// named/positional aggregates and through a vector field; GHDL swaps them
    /// where the design crosses fields.
    /// </summary>
    [Theory]
    [InlineData("rec_concurrent.vhdl", "rec", false)]
    [InlineData("rec_process.vhdl", "rec", false)]
    [InlineData("rec_idlist.vhdl", "idlist", false)]
    [InlineData("rec_aggnamed.vhdl", "aggnamed", false)]
    [InlineData("rec_aggpos.vhdl", "aggpos", false)]
    [InlineData("rec_cross_fields.vhdl", "nested", true)]
    [InlineData("rec_vecfield.vhdl", "vecfield", true)]
    public async Task First_record_repros_match_GHDL(string file, string entry, bool crossed)
    {
        var wf = await SimulateAsync(Fixture(file), entry, Drive("a", "a"), Drive("b", "b"));
        Assert.Equal(crossed ? 0 : 1, ValueAt(wf, "y", 10));
        Assert.Equal(crossed ? 1 : 0, ValueAt(wf, "z", 10));
        Assert.Equal(crossed ? 1 : 0, ValueAt(wf, "y", 30));
        Assert.Equal(crossed ? 0 : 1, ValueAt(wf, "z", 30));
    }

    [Fact]
    public async Task Record_port_fields_are_driven_one_by_one_and_shown_per_field()
    {
        var wf = await SimulateAsync(Fixture("rec_pkg.vhdl"), "rec_pkg",
            Port("ci.en", At(0, "1")), Port("ci.mode", At(0, "m_on")), Port("ci.d", At(0, "1010"), At(20, "0110")));
        Assert.Equal(0, ValueAt(wf, "co.en", 10));          // not ci.en
        Assert.Equal(0b0101, ValueAt(wf, "co.d", 10));      // d(0) & d(3 downto 1)
        Assert.Equal(0b10, ValueAt(wf, "s", 10));
        Assert.Equal(0b0011, ValueAt(wf, "co.d", 25));
        Assert.Equal(1, ValueAt(wf, "y", 25));
        Assert.Equal("m_off", wf.Signals.Single(s => s.Name == "co.mode").ValueAtNs(10).Label);
    }

    [Fact]
    public async Task Vcd_dumps_records_as_scopes_and_integers_as_integer_32()
    {
        var run = await RunAsync(Fixture("rec_int_enum.vhdl"), "rec_int_enum", 30, Drive("a", "a"), Drive("n", "n"));
        var vcd = run.History.ExportToVcd("rec_int_enum").Split('\n').Select(l => l.Trim()).ToList();
        int scope = vcd.IndexOf("$scope module c $end");
        Assert.True(scope > 0, "record c has its own scope");
        var vars = vcd.Skip(scope + 1).TakeWhile(l => l != "$upscope $end").Select(l => l.Split(' ')).ToList();
        Assert.Equal(["integer 32 cnt", "integer 32 lim", "reg 2 s", "wire 1 ok"],
            vars.Select(v => $"{v[1]} {v[2]} {v[4]}"));
        Assert.DoesNotContain(vcd, l => l.EndsWith(" idle $end"));   // enum literals are not signals

        var counter = await RunAsync(Fixture("int_vcd.vhdl"), "int_vcd", 50);
        Assert.Contains(counter.History.ExportToVcd("int_vcd").Split('\n'), l => l.Trim().StartsWith("$var integer 32 ") && l.Contains(" cnt "));
    }

    [Fact]
    public async Task An_unknown_field_is_an_analysis_error()
    {
        const string bad = """
            library ieee; use ieee.std_logic_1164.all;
            entity badfield is port (a : in std_logic; y : out std_logic); end entity;
            architecture rtl of badfield is
              type pair_t is record lo, hi : std_logic; end record;
              signal p : pair_t;
            begin
              p.lo <= a; y <= p.mid;
            end architecture;
            """;
        var store = new Kmila.Shared.Services.Api.SandboxProjectStore();
        var project = store.Create("bad-field", retain: false);
        try
        {
            project.WriteFile("badfield.vhdl", bad);
            var error = await Assert.ThrowsAsync<Parser.Models.ParserException>(() =>
                Kmila.Shared.Services.Api.HeadlessSimulator.RunProjectAsync(project.Path,
                    new Kmila.Shared.Services.Api.HeadlessRunOptions { EntryPoint = "badfield", TotalNs = 10, TickResNs = 1 }));
            Assert.Contains("no field \"mid\"", error.Message);
            Assert.Equal(7, error.Line);
        }
        finally { store.Delete(project.Id); }
    }
}
