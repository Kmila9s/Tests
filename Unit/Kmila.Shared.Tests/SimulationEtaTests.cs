using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Guards the run-time ETA (SimulationParameters.RemainingSeconds): it must gate
// early (no wild number in the first moments), estimate from the observed rate,
// hit 0 at completion, and clear when the run ends. Timing bounds are loose on
// purpose so the test stays stable under CI jitter.
public class SimulationEtaTests
{
    [Fact]
    public async Task RemainingSeconds_GatesEarly_ThenEstimatesFromRate()
    {
        var sim = new SimulationParameters();
        sim.BeginRun(projectId: 1);
        sim.SetState(SimState.Running);

        // <1% done → not estimable yet.
        sim.SetProgress(0.4);
        Assert.Null(sim.RemainingSeconds);

        // ~20% after ~0.4s → rate ~50%/s → ~1.6s remaining. Loose bounds.
        await Task.Delay(400);
        sim.SetProgress(20);
        Assert.NotNull(sim.RemainingSeconds);
        Assert.InRange(sim.RemainingSeconds!.Value, 0.4, 8.0);

        // Completion pins it to zero.
        sim.SetProgress(100);
        Assert.Equal(0, sim.RemainingSeconds);

        // Ending the run clears the estimate.
        sim.EndRun();
        Assert.Null(sim.RemainingSeconds);
    }

    [Fact]
    public void ElapsedSeconds_NullWhenIdle()
    {
        var sim = new SimulationParameters();
        Assert.Null(sim.ElapsedSeconds); // no run started
    }
}
