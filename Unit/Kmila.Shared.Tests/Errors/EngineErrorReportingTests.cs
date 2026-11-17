using Kmila.Shared.Services.Api;

namespace Kmila.Shared.Tests.Errors;

/// <summary>
/// G34/G35 (v3.1.0): an engine abort mid-run is reported to every head
/// instead of looking like a successful, empty waveform.
/// </summary>
public class EngineErrorReportingTests
{
    // A zero-delay combinational loop: GHDL stops it too (delta limit). It is a
    // genuine run-time error, so it stays one after every later engine fix.
    private const string Oscillator = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity osc is
          port (a : in std_logic; y : out std_logic);
        end entity osc;
        architecture rtl of osc is
          signal s : std_logic := '0';
        begin
          s <= not s;
          y <= s and a;
        end architecture rtl;
        """;

    /// <summary>Runs <paramref name="vhdl"/> through the simulate API in a throw-away sandbox under the temp folder.</summary>
    private static async Task<SimulateResponse> SimulateAsync(string vhdl)
    {
        var root = Directory.CreateTempSubdirectory("kmila-api-").FullName;
        try
        {
            var store = new SandboxProjectStore(root);
            var project = store.Create("engine-error", retain: false);
            project.WriteFile("design.vhdl", vhdl);
            return await new HeadlessSimulator(store).RunAsync(new SimulateRequest
            {
                ProjectId = project.Id, Duration = 20, TimeScale = "NS",
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task An_aborted_run_returns_the_engine_error_with_the_partial_waveform()
    {
        var response = await SimulateAsync(Oscillator);
        Assert.Contains("Delta cycle limit", response.Error);
        Assert.Contains(response.Log, l => l.StartsWith("[engine]"));
        Assert.StartsWith("$date", response.Vcd);
    }

    [Fact]
    public async Task A_completed_run_has_no_error()
    {
        var response = await SimulateAsync(TestDesigns.AndGate);
        Assert.Null(response.Error);
    }
}
