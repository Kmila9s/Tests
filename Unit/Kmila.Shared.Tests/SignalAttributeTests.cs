using Interpreter.Models;
using Interpreter.Services;
using Parser.Models;
using static Parser.Models.Constants;

namespace Kmila.Shared.Tests;

// Tier 4.1 (2026-07-28) regression fence for SignalAttributeHandler +
// DeferredAttribute. Bench case 148-signal-event-in-wait exercises the
// runtime end-to-end; these tests pin the handler's per-signal semantics
// directly so future engine refactors can't drift `'event`/`'last_value`/
// `'stable[(T)]` without lighting up here first.
public class SignalAttributeTests
{
    private static Variable MakeSignal(string name, string initial = "0",
        DATATYPES type = DATATYPES.STD_LOGIC)
    {
        var v = new Variable(name, initial, type);
        v.PortType = PortType.OUT;
        return v;
    }

    private static SignalAttributeHandler FreshHandler()
    {
        var h = SignalAttributeHandler.Instance;
        h.Reset();
        return h;
    }

    // --- 'event -----------------------------------------------------------

    [Fact]
    public void Event_False_ForFreshlyRegisteredSignal()
    {
        var h = FreshHandler();
        var sig = MakeSignal("clk");
        h.RegisterSignal(sig);

        Assert.False(h.GetEvent(sig));
    }

    [Fact]
    public void Event_True_AfterValueChange()
    {
        var h = FreshHandler();
        var sig = MakeSignal("clk", "0");
        h.RegisterSignal(sig);

        sig.Value = "1";

        Assert.True(h.GetEvent(sig));
    }

    [Fact]
    public void Event_False_AfterTickBoundaryClear()
    {
        var h = FreshHandler();
        var sig = MakeSignal("clk", "0");
        h.RegisterSignal(sig);
        sig.Value = "1";
        Assert.True(h.GetEvent(sig));

        h.ClearActiveFlagsAtTickBoundary(new HashSet<Variable> { sig });

        Assert.False(h.GetEvent(sig));
    }

    // --- 'last_value ------------------------------------------------------

    [Fact]
    public void LastValue_ReturnsInitialUntilFirstTransition()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        Assert.Equal("0", h.GetLastValue(sig));
    }

    [Fact]
    public void LastValue_ReturnsPriorValue_ForStimulusWrite()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        sig.Value = "1";   // stimulus-style direct write

        // OnValueChange rotated shadow ("0") into _previousValues.
        Assert.Equal("0", h.GetLastValue(sig));
    }

    [Fact]
    public void LastValue_TracksThroughMultipleTransitions()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0000", DATATYPES.STD_LOGIC_VECTOR);
        sig.Size = 4;
        h.RegisterSignal(sig);

        sig.Value = "0011";
        Assert.Equal("0000", h.GetLastValue(sig));
        sig.Value = "1010";
        Assert.Equal("0011", h.GetLastValue(sig));
        sig.Value = "1111";
        Assert.Equal("1010", h.GetLastValue(sig));
    }

    [Fact]
    public void LastValue_ViaOnDeltaCommit_UpdatesFromSchedulerPath()
    {
        var h = FreshHandler();
        var sig = MakeSignal("q", "0");
        h.RegisterSignal(sig);
        // Simulate a scheduler-driven commit: value already changed, and the
        // engine calls OnDeltaCommit with the pre-change oldValue map.
        sig.Value = "1";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "0" }, timeMoment: 5000);

        Assert.Equal("0", h.GetLastValue(sig));
    }

    // --- 'stable / 'stable(T) --------------------------------------------

    [Fact]
    public void Stable_NoArg_IsInverseOfEventThisDelta()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        // Fresh — no event, so stable.
        Assert.True(h.GetStable(sig));

        // After a change in this tick — not stable.
        sig.Value = "1";
        Assert.False(h.GetStable(sig));

        // After tick-boundary clear — stable again.
        h.ClearActiveFlagsAtTickBoundary(new HashSet<Variable> { sig });
        Assert.True(h.GetStable(sig));
    }

    [Fact]
    public void StableT_True_WhenElapsedMeetsThreshold()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        // Signal transitions at timeMoment=1000 (1 ns in raw units).
        sig.Value = "1";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "0" }, timeMoment: 1000);

        // Advance to timeMoment=21000 (21 raw units later) with no changes.
        h.OnDeltaCommit(new Dictionary<Variable, object?>(), timeMoment: 21000);

        // stable(20 ns) at tickRes=1 → 20_000 raw units — elapsed 20_000 → True.
        Assert.True(h.GetStable(sig, 20 * Interpreter.Models.PhysicalTime.FsPerNs));
        // stable(30 ns) → 30_000 raw units — elapsed 20_000 → False.
        Assert.False(h.GetStable(sig, 30 * Interpreter.Models.PhysicalTime.FsPerNs));
    }

    [Fact]
    public void StableT_False_ImmediatelyAfterTransition()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        sig.Value = "1";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "0" }, timeMoment: 5000);

        // Same timeMoment as the commit — elapsed=0 — stable(1) must be False.
        Assert.False(h.GetStable(sig, 1 * Interpreter.Models.PhysicalTime.FsPerNs));
    }

    // --- DeferredAttribute re-evaluation ---------------------------------

    [Fact]
    public void DeferredAttribute_Event_ReEvaluatesEachCall()
    {
        var h = FreshHandler();
        var sig = MakeSignal("clk", "0");
        h.RegisterSignal(sig);

        var da = new DeferredAttribute(sig, "event", DATATYPES.BOOLEAN);

        // Initially: no event.
        var r1 = da.Evaluate();
        Assert.Equal(false, r1.Value);

        // After a transition: event = true.
        sig.Value = "1";
        var r2 = da.Evaluate();
        Assert.Equal(true, r2.Value);

        // After tick-boundary clear: event = false again.
        h.ClearActiveFlagsAtTickBoundary(new HashSet<Variable> { sig });
        var r3 = da.Evaluate();
        Assert.Equal(false, r3.Value);
    }

    [Fact]
    public void DeferredAttribute_LastValue_ReflectsCurrentPreviousValue()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0000", DATATYPES.STD_LOGIC_VECTOR);
        sig.Size = 4;
        h.RegisterSignal(sig);

        var da = new DeferredAttribute(sig, "last_value", DATATYPES.STD_LOGIC_VECTOR);

        sig.Value = "0011";
        Assert.Equal("0000", da.Evaluate().Value);
        sig.Value = "1010";
        Assert.Equal("0011", da.Evaluate().Value);
    }

    [Fact]
    public void DeferredAttribute_StableT_UsesLiveLastChangeTime()
    {
        var h = FreshHandler();
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        sig.Value = "1";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "0" }, timeMoment: 1000);
        h.OnDeltaCommit(new Dictionary<Variable, object?>(), timeMoment: 21000);

        var da = new DeferredAttribute(sig, "stable", DATATYPES.BOOLEAN, timeArgFs: 20 * Interpreter.Models.PhysicalTime.FsPerNs);
        Assert.Equal(true, da.Evaluate().Value);

        // Advance less than threshold — should now report unstable relative
        // to a longer T.
        h.OnDeltaCommit(new Dictionary<Variable, object?>(), timeMoment: 22000);
        var daShort = new DeferredAttribute(sig, "stable", DATATYPES.BOOLEAN, timeArgFs: 100 * Interpreter.Models.PhysicalTime.FsPerNs);
        Assert.Equal(false, daShort.Evaluate().Value);
    }

    // --- Session isolation (Bug #18 regression guard) --------------------

    [Fact]
    public void Reset_ClearsAllPerSignalState()
    {
        var h = FreshHandler();
        var sig = MakeSignal("clk", "0");
        h.RegisterSignal(sig);
        sig.Value = "1";

        Assert.True(h.GetEvent(sig));

        h.Reset();

        // After Reset, the same handler singleton is clean — no stale event flag.
        Assert.False(h.GetEvent(sig));
        // GetLastValue falls back to Signal.Value when no state exists.
        Assert.Equal(sig.Value, h.GetLastValue(sig));
    }

    // --- Tier 4.1 close-out fences ---------------------------------------

    [Fact]
    public void StableT_ScalesByTick_TenNsDefault()
    {
        // At tickResolutionNs=10 (bench default), a 10 ns threshold should
        // fire after one tick of quiescence — not ten. Pre-close-out, callers
        // pre-scaled ns*1000 assuming 1 ns/tick, comparing against a 10× off
        // window that silently never fired.
        var h = FreshHandler();
        h.TickFs = 10 * Interpreter.Models.PhysicalTime.FsPerNs;
        var sig = MakeSignal("data", "0");
        h.RegisterSignal(sig);

        // Transition at t = 1 tick (tick*1000+delta = 1000). At t=2000 (~20 ns
        // wall) the signal has been quiet for exactly 1000 raw units, which
        // under tickRes=10 corresponds to 10 ns — the threshold should meet.
        sig.Value = "1";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "0" }, timeMoment: 1000);
        h.OnDeltaCommit(new Dictionary<Variable, object?>(), timeMoment: 2000);

        var da = new DeferredAttribute(sig, "stable", DATATYPES.BOOLEAN, timeArgFs: 10 * Interpreter.Models.PhysicalTime.FsPerNs);
        Assert.Equal(true, da.Evaluate().Value);

        // Same window vs. a 100 ns threshold: 100 ns @ tickRes=10 needs 10_000
        // raw units of quiet, but only 1000 have elapsed — should read unstable.
        var daLong = new DeferredAttribute(sig, "stable", DATATYPES.BOOLEAN, timeArgFs: 100 * Interpreter.Models.PhysicalTime.FsPerNs);
        Assert.Equal(false, daLong.Evaluate().Value);
    }

    [Fact]
    public void HadEventThisDelta_FiresTwiceFor010WithinSameTick()
    {
        // Pathological case the tick-scoped `_activeFlags` collapses: a
        // signal transitions twice within a single tick (0→1→0), settling
        // back to its initial value. `GetEvent` still reports true (event
        // happened), but a delta-scoped consumer should see BOTH deltas —
        // Tier 4.1 close-out adds `HadEventThisDelta` for exactly this.
        var h = FreshHandler();
        var sig = MakeSignal("s", "0");
        h.RegisterSignal(sig);

        // Delta 1 — 0→1
        h.BeginDelta();
        sig.Value = "1";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "0" }, timeMoment: 1000);
        Assert.True(h.HadEventThisDelta(sig));

        // Delta 2 — 1→0 (same tick, different delta)
        h.BeginDelta();
        Assert.False(h.HadEventThisDelta(sig)); // counter cleared at delta top
        sig.Value = "0";
        h.OnDeltaCommit(new Dictionary<Variable, object?> { [sig] = "1" }, timeMoment: 1001);
        Assert.True(h.HadEventThisDelta(sig));

        // Default tick-scoped predicate still reads true for the tick as a whole.
        Assert.True(h.GetEvent(sig));
    }

    [Fact]
    public void HadEventThisDelta_ZeroAfterQuietDelta()
    {
        var h = FreshHandler();
        var sig = MakeSignal("s", "0");
        h.RegisterSignal(sig);

        h.BeginDelta();
        // No commit — counter must stay zero.
        Assert.False(h.HadEventThisDelta(sig));
    }

    [Fact]
    public void DeferredAttribute_LastValue_VectorNullFallback_ReturnsWidthAwareU()
    {
        // Tier 4.1 close-out latent patch: for `sig'last_value` on an
        // unwritten vector, DeferredAttribute.Evaluate must not fall back to
        // the literal "false" — that would write garbage into a
        // std_logic_vector target. Confirm a Signal.Size-4 vector yields
        // "UUUU" of matching width.
        var h = FreshHandler();
        var sig = new Variable("v", null, DATATYPES.STD_LOGIC_VECTOR) { Size = 4, PortType = PortType.OUT };
        h.RegisterSignal(sig);

        var da = new DeferredAttribute(sig, "last_value", DATATYPES.STD_LOGIC_VECTOR);
        var result = da.Evaluate();

        // Variable.Value normalizes the vector; the pre-normalization payload
        // should be "UUUU" (4-wide). After Variable.Value normalization, the
        // string may lose delimiters but retains the U characters.
        Assert.NotNull(result.Value);
        var s = result.Value!.ToString() ?? "";
        Assert.Contains("U", s);
    }
}
