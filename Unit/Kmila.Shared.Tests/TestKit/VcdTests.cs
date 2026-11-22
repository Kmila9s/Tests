using Kmila.Shared.Services.Vcd;

namespace Kmila.Shared.Tests.TestKit;

/// <summary>
/// v3.0.7: the one VCD reader and waveform comparator (bench, test kit,
/// <c>kmila diff</c>, terminal viewer) on dumps shaped like GHDL's and Kmila's.
/// </summary>
public class VcdTests
{
    // GHDL layout: library scopes as siblings, hierarchical testbench, subscripts in the name, 1 fs.
    private const string Ghdl = """
        $date
          Sat Nov 21 21:10:35 2026
        $end
        $timescale
          1 fs
        $end
        $scope module standard $end
        $upscope $end
        $scope module std_logic_1164 $end
        $upscope $end
        $scope module tb $end
        $var reg 4 ! q[3:0] $end
        $var reg 1 " clk $end
        $scope module uut $end
        $var reg 4 # q[3:0] $end
        $upscope $end
        $upscope $end
        $enddefinitions $end
        #0
        bUUUU !
        0"
        bUUUU #
        #5000000
        1"
        b0001 !
        b0001 #
        #10000000
        0"
        """;

    // Kmila layout: one scope, 1 ps, delta cycles as +1 ps offsets, dumpvars block.
    private const string Kmila = """
        $date
           2026-11-21 20:49:32
        $end
        $timescale
           1ps
        $end
        $scope module counter $end
        $var reg 4 ! q $end
        $var reg 1 " clk $end
        $upscope $end
        $enddefinitions $end
        $dumpvars
        bUUUU !
        0"
        $end
        #5000
        1"
        #5001
        b1 !
        #10000
        0"
        """;

    [Fact]
    public void Reader_KeepsDeclarationOrderAndResolvesTheShallowestName()
    {
        var doc = VcdReader.Parse(Ghdl);
        Assert.Equal(1, doc.TimescaleFs);
        Assert.Equal("tb", doc.Module); // library scopes are not the module
        Assert.Equal(["!", "\"", "#"], doc.Signals.Select(s => s.Id));
        Assert.Equal("q[3:0]", doc.Signals[0].DisplayName);
        Assert.Equal("!", doc.ByName["Q"].Id); // testbench q (depth 1) over uut.q (depth 2), case-insensitive
        Assert.Equal([new VcdChange(0, "UUUU"), new VcdChange(5_000_000, "0001")], doc.ByName["q"].Transitions);
    }

    [Fact]
    public void Reader_ReadsKmilaDumpsInFemtoseconds()
    {
        var doc = VcdReader.Parse(Kmila);
        Assert.Equal(1_000, doc.TimescaleFs);
        Assert.Equal("counter", doc.Module);
        Assert.Equal([new VcdChange(0, "UUUU"), new VcdChange(5_001_000, "1")], doc.ByName["q"].Transitions);
        Assert.Equal(10_000_000, doc.ByName["clk"].Transitions[^1].TimeFs);
    }

    [Fact]
    public void Strict_SnapsDeltasPadsVectorsAndCountsT0()
    {
        var diffs = WaveformComparer.Compare(VcdReader.Parse(Ghdl), VcdReader.Parse(Kmila),
            CompareOptions.Strict(["q", "clk"], ignoreT0: false));
        Assert.All(diffs, d => Assert.True(d.Equal, d.Signal)); // b1 at 5.001 ns = 0001 at 5 ns

        var changed = Kmila.Replace("bUUUU !", "b0000 !");
        var q = WaveformComparer.Compare(VcdReader.Parse(Ghdl), VcdReader.Parse(changed),
            CompareOptions.Strict(["q"], ignoreT0: false)).Single();
        Assert.Equal(1, q.Mismatches);
        Assert.Equal(new ValueMismatch(0, "UUUU", "0000"), q.Examples.Single());

        var ignored = WaveformComparer.Compare(VcdReader.Parse(Ghdl), VcdReader.Parse(changed),
            CompareOptions.Strict(["q"], ignoreT0: true)).Single();
        Assert.True(ignored.Equal);
    }

    [Fact]
    public void Strict_ReportsWhichSideLacksAPort()
    {
        var diffs = WaveformComparer.Compare(VcdReader.Parse(Ghdl), VcdReader.Parse(Kmila),
            CompareOptions.Strict(["clk", "y", "uut"], ignoreT0: false));
        Assert.Equal(MissingSide.None, diffs[0].Missing);
        Assert.Equal(MissingSide.Both, diffs[1].Missing);
        Assert.False(diffs[1].Equal);
        Assert.Equal("missing from golden and kmila", diffs[1].MissingFrom("golden", "kmila"));
        Assert.Null(diffs[0].MissingFrom("golden", "kmila"));
    }

    [Fact]
    public void NumericBuses_ComparesCleanBinaryByValueAndListsOneSidedSignals()
    {
        const string A = "$timescale 1ns $end $var wire 4 ! v $end $var wire 1 \" only_a $end $enddefinitions $end #0 b0011 ! 1\" #10 bX011 !";
        const string B = "$timescale 1ns $end $var wire 4 ! V $end $enddefinitions $end #0 b11 ! #10 b0011 !";
        var diffs = WaveformComparer.Compare(VcdReader.Parse(A), VcdReader.Parse(B),
            new CompareOptions { Equality = ValueEquality.NumericBuses, MaxExamples = int.MaxValue });
        var v = diffs.Single(d => d.Signal == "v");
        Assert.Equal(1, v.Mismatches); // 0011 = 11 at 0; X011 ≠ 0011 at 10 ns
        Assert.Equal(new ValueMismatch(10_000_000, "X011", "0011"), v.Examples.Single());
        Assert.Equal(MissingSide.Actual, diffs.Single(d => d.Signal == "only_a").Missing);
    }
}
