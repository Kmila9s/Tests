using Kmila.Shared.Services.Vcd;
using Kmila.TestKit.Harness;

namespace Kmila.Shared.Tests.TestKit;

/// <summary>v3.0.9: the testbench that replays Kmila's stimulus on a reference simulator, and sampling at graded instants.</summary>
public class HarnessTests
{
    private static readonly HarnessSpec Spec = new(
        "counter",
        [new("clk", 1, false), new("d", 4, true), new("q", 4, true)],
        [new("clk", 10, false)],
        [new(0, "d", "0011"), new(0, "clk", "0"), new(25, "d", "1100")],
        new Dictionary<string, string> { ["d"] = "0000" },
        100);

    [Fact]
    public void Vhdl_DeclaresPortsInstantiatesTheDutAndReplaysTheStimulus()
    {
        string tb = new VhdlHarnessGenerator().Generate(Spec);
        Assert.Contains("entity kmila_harness is", tb);
        Assert.Contains("signal d : std_logic_vector(3 downto 0) := \"0000\";", tb);
        Assert.Contains("signal clk : std_logic;", tb);
        Assert.Contains("dut: entity work.counter port map (", tb);
        Assert.Contains("    q => q", tb);
        Assert.Contains("      wait for 10 ns;\n      clk <= not clk;", tb);
        // Time-0 values, then the 25 ns change, then wait forever.
        Assert.Matches(@"d <= ""0011"";\s+clk <= '0';\s+wait for 25 ns;\s+d <= ""1100"";\s+wait;", tb);
    }

    [Fact]
    public void Vhdl_RefusesValuesItWouldHaveToReinterpret()
    {
        var gen = new VhdlHarnessGenerator();
        Assert.Throws<NotSupportedException>(() => gen.Generate(Spec with { Events = [new(0, "d", "12")] }));
        Assert.Throws<NotSupportedException>(() => gen.Generate(Spec with { Events = [new(0, "d", "011")] }));
    }

    [Fact]
    public void Comparer_SamplesOnlyAtTheGivenInstants()
    {
        const string A = "$timescale 1ns $end $var wire 1 ! y $end $enddefinitions $end #0 0! #10 1! #20 0!";
        const string B = "$timescale 1ns $end $var wire 1 ! y $end $enddefinitions $end #0 0! #10 0! #20 0!";
        var options = new CompareOptions { Signals = ["y"], InitialValue = "U", SampleTimesFs = [5_000_000, 25_000_000] };
        var d = WaveformComparer.Compare(VcdReader.Parse(A), VcdReader.Parse(B), options).Single();
        Assert.Equal((2, 0), (d.Samples, d.Mismatches)); // they only differ between 10 and 20 ns
        d = WaveformComparer.Compare(VcdReader.Parse(A), VcdReader.Parse(B), options with { SampleTimesFs = [15_000_000] }).Single();
        Assert.Equal(1, d.Mismatches);
    }
}
