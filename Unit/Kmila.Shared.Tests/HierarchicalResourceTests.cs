using System.Collections.Generic;
using System.IO;
using System.Linq;
using Interpreter.Services;
using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Hierarchical resource estimation (2026-10-23). Locks the behaviour that a
// structural top's resource usage aggregates its port-mapped sub-components,
// instead of reporting only its own I/O.
//
//   struct_top  → adder8 + reg8        : the reg8's 8 flip-flops roll up
//   two_regs    → reg8 (x2)            : per-instance counting → 16 FFs
//   generic_top → reg_n generic(N=>16) : generic override scales to 16 FFs
//   default_top → reg_n (no map)       : default generic → 8 FFs
//   reg8        → (leaf)               : self-usage unchanged under aggregation
//
// Fixtures live under Fixtures/Hierarchy/*.vhd (copied next to the test
// assembly). The whole suite runs serialized (see AssemblyInfo.cs) because the
// estimator leans on the process-global EntityRegistry singleton.
public class HierarchicalResourceTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "Hierarchy");

    // Parses the whole fixture design, then aggregates the named top entity's
    // subtree usage exactly as the app's post-parse pass / `kmila fit` do.
    private static Resources Aggregate(string topName)
    {
        var files = Directory.GetFiles(FixtureDir, "*.vhd");
        var byName = HierarchyLoader.ParseDesign(files, topName, out var top, out var specialize);
        Assert.NotNull(top);
        return Resources.ComputeHierarchical(top!, byName, specialize);
    }

    [Fact]
    public void StructuralTop_RollsUpChildFlipFlops()
    {
        var r = Aggregate("struct_top");
        // reg8 contributes 8 FFs; adder8 is purely combinational.
        Assert.Equal(8, r.FlipFlops);
        // I/O is the TOP entity's own ports only (clk + x + y + outp = 25),
        // never the internal child ports.
        Assert.Equal(25, r.IOPins);
        // The child's clocked process surfaces one clock domain.
        Assert.Equal(1, r.ClockDomains);
    }

    [Fact]
    public void RepeatedInstances_AreCountedPerInstance()
    {
        var r = Aggregate("two_regs");
        // Two reg8 instances = two hardware copies = 16 FFs.
        Assert.Equal(16, r.FlipFlops);
    }

    [Fact]
    public void GenericMapOverride_ScalesChildWidth()
    {
        var r = Aggregate("generic_top");
        // reg_n instantiated at N => 16 must count 16 FFs, not its default 8.
        Assert.Equal(16, r.FlipFlops);
    }

    [Fact]
    public void NoGenericMap_UsesChildDefaults()
    {
        var r = Aggregate("default_top");
        // reg_n with no generic map falls back to its declared default N = 8.
        Assert.Equal(8, r.FlipFlops);
    }

    [Fact]
    public void LeafEntity_AggregatesToItsOwnSelfUsage()
    {
        // A leaf (no instantiations) aggregates to exactly its self estimate.
        var files = Directory.GetFiles(FixtureDir, "*.vhd");
        var byName = HierarchyLoader.ParseDesign(files, "reg8", out var top, out var specialize);
        Assert.NotNull(top);
        var hier = Resources.ComputeHierarchical(top!, byName, specialize);
        Assert.Equal(top!.ResourceUsage.FlipFlops, hier.FlipFlops);
        Assert.Equal(8, hier.FlipFlops);
    }
}
