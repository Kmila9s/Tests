using Interpreter.Services;
using Kmila.Shared.Services;
using TimeMachine.Models;

namespace Kmila.Shared.Tests;

// Acceptance gate for the event-driven FastSimulationRunner (Fast run mode).
// The fast runner skips idle ticks; it is only sound if it produces the SAME
// waveform as the reference SimulationRunner. These tests run representative
// designs through BOTH runners and assert byte-identical VCD, covering the
// three event sources the fast runner must never jump over: clock edges,
// timed process wakes (`wait for`), and edge-triggered processes / scheduled
// signal updates. If any design diverges, Fast mode must stay disabled until
// the fast runner is fixed.
public class FastRunnerEquivalenceTests
{
    private enum RunnerKind { Reference, Fast }

    // Load + build config + run, returning the exported VCD. Each call starts
    // from clean global state (the front-end resets the registries) and a fresh
    // parse, so the ONLY difference between a Reference and a Fast call for the
    // same source is which runner drove the identical config.
    private static async Task<string> RunVcdAsync(string vhdl, string entityName,
        long totalNs, int tickResNs, RunnerKind kind)
    {
        var entity = TestDesigns.LoadTop(vhdl, entityName);
        var config = StimulusBuilder.BuildConfigNs(entity, portMoments: null,
            totalNs: totalNs, tickResNs: tickResNs, systemClockMhz: 50);

        SignalHistory history;
        if (kind == RunnerKind.Reference)
        {
            using var runner = new SimulationRunner();
            runner.OnEngineReady += eng => eng.TickResolutionNs = tickResNs;
            await runner.RunAsync(config);
            history = runner.Engine!.History;
        }
        else
        {
            using var runner = new FastSimulationRunner();
            runner.OnEngineReady += eng => eng.TickResolutionNs = tickResNs;
            await runner.RunAsync(config);
            history = runner.Engine!.History;
        }

        return history.ExportToVcd(entity.Name, timescaleNs: tickResNs);
    }

    private static async Task AssertRunnersAgreeAsync(string vhdl, string entity,
        long totalNs, int tickResNs = 10)
    {
        // Reference first, then Fast — each from clean state (see RunVcdAsync).
        var reference = await RunVcdAsync(vhdl, entity, totalNs, tickResNs, RunnerKind.Reference);
        var fast      = await RunVcdAsync(vhdl, entity, totalNs, tickResNs, RunnerKind.Fast);

        Assert.False(string.IsNullOrWhiteSpace(reference));
        Assert.Equal(WithoutDate(reference), WithoutDate(fast));
    }

    /// <summary>The VCD without its <c>$date</c> block (the two runs may straddle a second): the golden-master rule.</summary>
    private static string WithoutDate(string vcd) => Kmila.TestKit.GoldenHashes.WithoutDate(vcd);

    // Clock-only design: exercises clock-edge jumping. The auto-clock drives
    // `clk`; the fast runner must land on every rising/falling edge tick.
    [Fact]
    public async Task ClockOnly_FastMatchesReference()
    {
        const string vhdl = @"
entity clkonly is
  port (
    clk : in std_logic;
    q   : out std_logic
  );
end clkonly;

architecture rtl of clkonly is
begin
  process(clk)
  begin
    if rising_edge(clk) then
      q <= '1';
    end if;
  end process;
end rtl;";
        await AssertRunnersAgreeAsync(vhdl, "clkonly", totalNs: 1000);
    }

    // Edge-triggered D flip-flop: exercises process wake on a signal change
    // plus a scheduled (`<=`) signal update. std_logic only (no numeric lib).
    [Fact]
    public async Task DFlipFlop_FastMatchesReference()
    {
        const string vhdl = @"
entity dff is
  port (
    clk : in std_logic;
    d   : in std_logic;
    q   : out std_logic
  );
end dff;

architecture rtl of dff is
begin
  process(clk)
  begin
    if rising_edge(clk) then
      q <= d;
    end if;
  end process;
end rtl;";
        await AssertRunnersAgreeAsync(vhdl, "dff", totalNs: 1000);
    }

    // Free-running process with `wait for`: exercises timed process-wake
    // jumping (_pendingProcessWakes) — the source with no scheduler entry.
    [Fact]
    public async Task WaitFor_FastMatchesReference()
    {
        const string vhdl = @"
entity blink is
  port (
    led : out std_logic
  );
end blink;

architecture rtl of blink is
begin
  process
  begin
    led <= '0';
    wait for 30 ns;
    led <= '1';
    wait for 30 ns;
  end process;
end rtl;";
        await AssertRunnersAgreeAsync(vhdl, "blink", totalNs: 500);
    }
}
