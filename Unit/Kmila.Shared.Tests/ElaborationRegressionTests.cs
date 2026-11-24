using Kmila.Shared.Models;
using Kmila.Shared.Services.Api;

namespace Kmila.Shared.Tests;

/// <summary>
/// GAP-ELAB-001 end to end: a structural top closed with <c>end entity;</c>
/// (valid VHDL-93, previously not flattened, so the output stayed undriven)
/// simulates its sub-component through the headless pipeline.
/// </summary>
public class ElaborationRegressionTests
{
    private const string StructuralTop = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity top is
          port (x, z : in std_logic; q : out std_logic);
        end entity;
        architecture structural of top is
          component and2
            port (a, b : in std_logic; y : out std_logic);
          end component;
        begin
          u0 : and2 port map (a => x, b => z, y => q);
        end architecture;
        """;

    [Fact]
    public async Task Structural_top_closed_with_end_entity_drives_its_output()
    {
        var store = new SandboxProjectStore();
        var project = store.Create("elab-gap", retain: false);
        try
        {
            project.WriteFile("and2.vhd", TestDesigns.AndGate);
            project.WriteFile("top.vhd", StructuralTop);
            static PortMomentValue At(int ns, string v) => new() { Time = ns, TimeScale = "NS", Value = v };
            var run = await HeadlessSimulator.RunProjectAsync(project.Path, new HeadlessRunOptions
            {
                EntryPoint = "top",
                TotalNs = 100,
                TickResNs = 10,
                PortMoments =
                [
                    new PortMomentConfiguration { PortName = "x", Values = [At(0, "0"), At(40, "1")] },
                    new PortMomentConfiguration { PortName = "z", Values = [At(0, "1"), At(70, "0")] },
                ],
            });

            Assert.Contains(run.Log, l => l.StartsWith("[elab] inlined components for top"));
            var wf = WaveformData.FromSignalHistory(run.History, run.TickFs);
            var q = wf.Signals.Single(s => s.Name.Equals("q", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, q.ValueAtNs(20).Value);   // x=0, z=1
            Assert.Equal(1, q.ValueAtNs(50).Value);   // x=1, z=1
            Assert.Equal(0, q.ValueAtNs(90).Value);   // x=1, z=0
        }
        finally { store.Delete(project.Id); }
    }
}
