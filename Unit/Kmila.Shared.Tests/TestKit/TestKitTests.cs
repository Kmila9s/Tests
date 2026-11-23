using Kmila.Shared.Services;
using Kmila.Shared.Services.Api;
using Kmila.Shared.Services.Vcd;
using Kmila.TestKit;
using Kmila.TestKit.Reference;
using Kmila.TestKit.Runners;

namespace Kmila.Shared.Tests.TestKit;

/// <summary>v3.0.7: the test kit shared by the bench, the practice validator and these tests.</summary>
public class TestKitTests
{
    private const string AndGate = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity and_gate is
          port (a, b : in std_logic; y : out std_logic);
        end entity;
        architecture rtl of and_gate is
        begin
          y <= a and b;
        end architecture;
        """;

    [Fact]
    public void GoldenHashes_IgnoreTheDateBlockOnly()
    {
        const string Body = "$timescale\n   1ps\n$end\n#0\n1!\n";
        string a = GoldenHashes.Of("$date\n   2026-11-21 20:49:32\n$end\n" + Body);
        string b = GoldenHashes.Of("$date\n   2026-11-22 08:00:00\n$end\n" + Body);
        Assert.Equal(a, b);
        Assert.Equal(GoldenHashes.Of(Body), a); // same bytes as `tail -n +4`
        Assert.NotEqual(a, GoldenHashes.Of("$date\n x\n$end\n" + Body.Replace("1!", "0!")));
    }

    [Fact]
    public void GoldenHashes_ReportChangedMissingAndNewCases()
    {
        var baseline = GoldenHashes.Parse(GoldenHashes.Format(new Dictionary<string, string> { ["001"] = "aa", ["002"] = "bb", ["003"] = "cc" }));
        var current = new Dictionary<string, string> { ["001"] = "aa", ["002"] = "XX", ["004"] = "dd" };
        Assert.Equal(["002: changed", "003: missing (no VCD)", "004: not in the baseline"], GoldenHashes.Compare(baseline, current));
        Assert.Empty(GoldenHashes.Compare(baseline, baseline));
    }

    [Fact]
    public void Ghdl_CopyGetsSpacedTimeUnitsOnly()
    {
        Assert.Equal("wait for 10 ns; x <= '1' after 2 ps;", GhdlReferenceSimulator.SpaceTimeUnits("wait for 10ns; x <= '1' after 2 ps;"));
        Assert.Equal("signal ns1 : bit;", GhdlReferenceSimulator.SpaceTimeUnits("signal ns1 : bit;"));
        Assert.Equal("constant clk_10ns : time := 1.5 us;", GhdlReferenceSimulator.SpaceTimeUnits("constant clk_10ns : time := 1.5us;"));
    }

    [Fact]
    public async Task InProcessRunner_SimulatesLikeTheApiAndLeavesNoProject()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "kmila-bench", "sandbox");
        int before = Directory.Exists(sandbox) ? Directory.GetDirectories(sandbox).Length : 0;
        var response = await new InProcessKmilaRunner().SimulateAsync(
            [new SourceFile("and_gate.vhdl", AndGate)],
            new SimulateRequest
            {
                EntryPoint = "and_gate",
                Duration = 40,
                PortMoments =
                [
                    new ApiPortMoment { Port = "a", Time = 0, Value = "1" },
                    new ApiPortMoment { Port = "b", Time = 0, Value = "0" },
                    new ApiPortMoment { Port = "b", Time = 20, Value = "1" },
                ],
            });
        Assert.Null(response.Error);
        Assert.Equal("and_gate", response.Entity);
        var y = VcdReader.Parse(response.Vcd).ByName["y"].Transitions;
        Assert.Equal("0", y.Last(c => c.TimeFs <= 10_000_000).Value);
        Assert.Equal("1", y.Last(c => c.TimeFs <= 30_000_000).Value);
        Assert.Equal(before, Directory.GetDirectories(sandbox).Length);
    }

    [Fact]
    public void SharedContentPaths_FindsTheBundledContent()
    {
        var practice = SharedContentPaths.Find("Practice");
        Assert.NotNull(practice);
        Assert.True(File.Exists(Path.Combine(practice, "manifest.json")));
        Assert.Null(SharedContentPaths.Find("no-such-folder-v307"));
    }
}
