using Interpreter.Models;
using Interpreter.Services;
using Interpreter.Synthesis;
using Parser.Models;
using static Parser.Models.Constants;

namespace Interpreter.Tests;

/// <summary>
/// End-to-end coverage that goes from a real-shaped Operation tree (the
/// kind <c>Operation.Synthesize</c> produces) through
/// <see cref="ConcurrentAssignSynthesizer.SynthesizeInto"/> and asserts
/// the resulting netlist contains the IEEE primitive cells. Proves the
/// v3.1 wiring (extended <see cref="DeferredFunction.ShouldDefer"/>) is
/// reaching the synth path, not just the unit-tested table.
/// </summary>
public static class IEEELoweringEndToEndTests
{
    /// <summary>Runs the suite. Returns 0 on full pass, otherwise the failed-test count.</summary>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== IEEE Lowering End-to-End Tests ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("rising_edge synthesizes EDGE_DETECT",                    Test_RisingEdgeIntoNetlist),
            ("to_integer synthesizes TO_INTEGER",                       Test_ToIntegerIntoNetlist),
            ("resize synthesizes RESIZE",                               Test_ResizeIntoNetlist),
            ("shift_left synthesizes SHL",                              Test_ShiftLeftIntoNetlist),
            ("rotate_right synthesizes ROR",                            Test_RotateRightIntoNetlist),
            ("ShouldDefer expanded to cover all IEEE primitives",       Test_ShouldDeferCovers),
        };

        foreach (var (name, run) in tests)
        {
            try
            {
                bool ok = run();
                Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}");
                if (!ok) failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [FAIL] {name} — exception: {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"Results: {tests.Length - failed} passed, {failed} failed");
        return failed;
    }

    // ---- Helpers --------------------------------------------------------

    /// <summary>
    /// Builds a minimal <see cref="SynthesisContext"/> with one input port,
    /// runs <see cref="ConcurrentAssignSynthesizer.SynthesizeInto"/> with a
    /// hand-crafted item list, and returns the resulting netlist.
    /// </summary>
    private static Netlist SynthOneOp(IReadOnlyList<object> items, Variable resultVar, Variable input, int inputWidth = 8)
    {
        var ctx = new SynthesisContext("test_entity");
        ctx.AddPort(input);
        // Result net for the assignment LHS.
        var outNet = ctx.GetOrCreateNet(
            resultVar.Name,
            inputWidth > 1 ? NetKind.Bus : NetKind.Wire,
            inputWidth, NetOrigin.Signal);

        // Carrier text must only reference declared names (the undeclared-
        // identifier check rejects anything else); its items are replaced below.
        var op = new Operation(input.Name, new HashSet<Variable> { input, resultVar });
        // Bypass parsing — push the items directly. This is what the
        // expression parser would produce after my v3.1 change.
        SetItems(op, items);

        ConcurrentAssignSynthesizer.SynthesizeInto(op, outNet, ctx);
        return ctx.Build();
    }

    private static void SetItems(Operation op, IReadOnlyList<object> items)
    {
        // Operation.Items is a public IReadOnlyList<object> — but the
        // backing field is set during Synthesize. Use reflection to
        // inject our hand-crafted list for end-to-end testing.
        var t = typeof(Operation);
        var field = t.GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? t.GetProperty("Items")?.GetSetMethod(true)?.DeclaringType
                       ?.GetField($"<{nameof(Operation.Items)}>k__BackingField",
                                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            // Property has no setter — work around via the property's
            // backing field name pattern produced by the C# compiler.
            field = t.GetField($"<{nameof(Operation.Items)}>k__BackingField",
                               System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }
        if (field != null)
        {
            // Operation.Items is typed object[] not List<object>; convert.
            field.SetValue(op, items.ToArray());
        }
        else
        {
            throw new InvalidOperationException("Could not inject test items into Operation");
        }
    }

    private static Variable MkPort(string name, int width = 8)
        => new Variable(name, "0", DATATYPES.STD_LOGIC_VECTOR, width)
        {
            IsPort = true,
            PortType = PortType.IN,
        };

    private static Variable MkSignal(string name, int width = 8)
        => new Variable(name, "0", DATATYPES.STD_LOGIC_VECTOR, width);

    // ---- End-to-end coverage --------------------------------------------

    private static bool Test_RisingEdgeIntoNetlist()
    {
        var clk = new Variable("clk", "0", DATATYPES.STD_LOGIC, 1) { IsPort = true, PortType = PortType.IN };
        var q   = MkSignal("q", width: 1);
        var df  = new DeferredFunction("rising_edge", new object[] { clk },
            DATATYPES.BOOLEAN, new HashSet<Variable> { clk });
        var nl = SynthOneOp(new object[] { df }, q, clk, inputWidth: 1);
        return nl.Cells.Any(c => c.Kind == CellKind.EDGE_DETECT);
    }

    private static bool Test_ToIntegerIntoNetlist()
    {
        var addr = MkPort("addr", width: 8);
        var idx  = MkSignal("idx", width: 32);
        var df   = new DeferredFunction("to_integer", new object[] { addr },
            DATATYPES.INTEGER, new HashSet<Variable> { addr });
        var nl = SynthOneOp(new object[] { df }, idx, addr, inputWidth: 8);
        return nl.Cells.Any(c => c.Kind == CellKind.TO_INTEGER);
    }

    private static bool Test_ResizeIntoNetlist()
    {
        var src    = MkPort("src", width: 4);
        var widen  = MkSignal("widen", width: 16);
        var widthM = MkSignal("w", width: 16);
        var df     = new DeferredFunction("resize", new object[] { src, widthM },
            DATATYPES.STD_LOGIC_VECTOR, new HashSet<Variable> { src, widthM });
        var nl = SynthOneOp(new object[] { df }, widen, src, inputWidth: 4);
        return nl.Cells.Any(c => c.Kind == CellKind.RESIZE);
    }

    private static bool Test_ShiftLeftIntoNetlist()
    {
        var src = MkPort("src", width: 8);
        var n   = MkSignal("n", width: 1);
        var dst = MkSignal("dst", width: 8);
        var df  = new DeferredFunction("shift_left", new object[] { src, n },
            DATATYPES.STD_LOGIC_VECTOR, new HashSet<Variable> { src, n });
        var nl = SynthOneOp(new object[] { df }, dst, src, inputWidth: 8);
        return nl.Cells.Any(c => c.Kind == CellKind.SHL);
    }

    private static bool Test_RotateRightIntoNetlist()
    {
        var src = MkPort("src", width: 8);
        var n   = MkSignal("n", width: 1);
        var dst = MkSignal("dst", width: 8);
        var df  = new DeferredFunction("rotate_right", new object[] { src, n },
            DATATYPES.STD_LOGIC_VECTOR, new HashSet<Variable> { src, n });
        var nl = SynthOneOp(new object[] { df }, dst, src, inputWidth: 8);
        return nl.Cells.Any(c => c.Kind == CellKind.ROR);
    }

    private static bool Test_ShouldDeferCovers()
    {
        // Every name that has a primitive emitter must also be deferred,
        // otherwise the synth path never sees the call. Type-cast
        // overloads (unsigned/signed/std_logic_vector) are intentionally
        // NOT deferred — they're documented in DeferredFunction.ShouldDefer.
        string[] mustDefer =
        {
            "rising_edge", "falling_edge",
            "to_integer", "conv_integer",
            "to_unsigned", "to_signed",
            "resize",
            "shift_left", "shift_right",
            "rotate_left", "rotate_right",
        };
        foreach (var n in mustDefer)
            if (!DeferredFunction.ShouldDefer(n)) return false;
        return true;
    }
}
