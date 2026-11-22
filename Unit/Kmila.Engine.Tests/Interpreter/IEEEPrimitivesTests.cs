using Interpreter.Synthesis;

namespace Interpreter.Tests;

/// <summary>
/// Coverage for the IEEE primitive emitter table — verifies that every
/// registered emitter produces the right cell kind, output width, and
/// attribute payload, and that the lookup machinery handles canonical
/// names, aliases, and unknown names correctly.
/// </summary>
public static class IEEEPrimitivesTests
{
    /// <summary>Runs the suite. Returns 0 on full pass, otherwise the failed-test count.</summary>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== IEEE Primitives Tests ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("Lookup: canonical names resolve",                   Test_LookupCanonical),
            ("Lookup: aliases resolve",                           Test_LookupAliases),
            ("Lookup: unknown name returns null",                 Test_LookupUnknown),
            ("rising_edge emits EDGE_DETECT cell",                Test_RisingEdge),
            ("falling_edge emits EDGE_DETECT cell with attr",     Test_FallingEdge),
            ("to_integer emits TO_INTEGER cell, 32-bit output",   Test_ToInteger),
            ("conv_integer (alias) emits TO_INTEGER",             Test_ConvInteger),
            ("to_unsigned emits TO_VECTOR with Signed=false",     Test_ToUnsigned),
            ("to_signed emits TO_VECTOR with Signed=true",        Test_ToSigned),
            ("resize emits RESIZE cell with width attrs",         Test_Resize),
            ("shift_left emits SHL cell",                         Test_ShiftLeft),
            ("shift_right emits SHR cell",                        Test_ShiftRight),
            ("rotate_left emits ROL cell",                        Test_RotateLeft),
            ("rotate_right emits ROR cell",                       Test_RotateRight),
            ("unsigned/signed/std_logic_vector type-cast → BUF",  Test_TypeCasts),
            ("TryLower returns null for unknown function",        Test_TryLowerUnknown),
            ("Output net width matches the emitter's contract",   Test_OutputWidths),
            ("AllRegisteredNames includes core IEEE set",         Test_AllNamesIncludesCore),
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
    /// Builds a one-shot SynthesisContext + a single bus input net so each
    /// test can lower an emitter and inspect the resulting cells without
    /// dragging in the rest of the parser pipeline.
    /// </summary>
    private static (SynthesisContext ctx, Net inputBus) MakeContext(int inputWidth = 8)
    {
        var ctx = new SynthesisContext("test_entity");
        var bus = ctx.GetOrCreateNet("input_bus",
            inputWidth > 1 ? NetKind.Bus : NetKind.Wire,
            inputWidth, NetOrigin.PortIn);
        return (ctx, bus);
    }

    private static IEEEPrimitiveResult Lower(string fnName, IReadOnlyList<Net> args, SynthesisContext ctx)
    {
        var primitive = IEEEPrimitives.Lookup(fnName);
        if (primitive == null) throw new InvalidOperationException($"No primitive for '{fnName}'");
        return primitive.Lower(new IEEEPrimitiveArgs(args, ctx, SourceLine: null));
    }

    // ---- Lookup tests ---------------------------------------------------

    private static bool Test_LookupCanonical()
    {
        string[] canonical =
        {
            "rising_edge", "falling_edge",
            "to_integer", "to_unsigned", "to_signed",
            "resize",
            "shift_left", "shift_right", "rotate_left", "rotate_right",
            "unsigned", "signed", "std_logic_vector"
        };
        foreach (var n in canonical)
            if (IEEEPrimitives.Lookup(n) is null) return false;
        return true;
    }

    private static bool Test_LookupAliases()
    {
        return IEEEPrimitives.Lookup("conv_integer") is not null
            && IEEEPrimitives.Lookup("conv_std_logic_vector") is not null
            // Case-insensitive lookup must work too.
            && IEEEPrimitives.Lookup("RISING_EDGE") is not null;
    }

    private static bool Test_LookupUnknown()
        => IEEEPrimitives.Lookup("not_a_real_function") is null;

    // ---- Emitter tests --------------------------------------------------

    private static bool Test_RisingEdge()
    {
        var (ctx, clk) = MakeContext(inputWidth: 1);
        var r = Lower("rising_edge", new[] { clk }, ctx);
        var nl = ctx.Build();
        return nl.Cells.Count == 1
            && nl.Cells[0].Kind == CellKind.EDGE_DETECT
            && r.OutputNet.Width == 1
            && r.EmittedCells == 1;
    }

    private static bool Test_FallingEdge()
    {
        var (ctx, clk) = MakeContext(inputWidth: 1);
        Lower("falling_edge", new[] { clk }, ctx);
        var nl = ctx.Build();
        var c = nl.Cells.SingleOrDefault();
        return c is not null
            && c.Kind == CellKind.EDGE_DETECT
            && c.Attrs is not null
            && c.Attrs.TryGetValue("Edge", out var edge) && edge == "falling";
    }

    private static bool Test_ToInteger()
    {
        var (ctx, bus) = MakeContext(inputWidth: 8);
        var r = Lower("to_integer", new[] { bus }, ctx);
        var nl = ctx.Build();
        var c = nl.Cells.SingleOrDefault(x => x.Kind == CellKind.TO_INTEGER);
        return c is not null && r.OutputNet.Width == 32 && c.Attrs!["Width"] == "8";
    }

    private static bool Test_ConvInteger()
    {
        var (ctx, bus) = MakeContext(inputWidth: 4);
        Lower("conv_integer", new[] { bus }, ctx);   // alias path
        var nl = ctx.Build();
        return nl.Cells.Any(c => c.Kind == CellKind.TO_INTEGER);
    }

    private static bool Test_ToUnsigned()
    {
        var (ctx, intIn) = MakeContext(inputWidth: 32);
        var widthMarker = ctx.GetOrCreateNet("w", NetKind.Bus, 8, NetOrigin.Constant);
        Lower("to_unsigned", new[] { intIn, widthMarker }, ctx);
        var c = ctx.Build().Cells.SingleOrDefault(x => x.Kind == CellKind.TO_VECTOR);
        return c is not null && c.Attrs!["Signed"] == "false" && c.Attrs["Width"] == "8";
    }

    private static bool Test_ToSigned()
    {
        var (ctx, intIn) = MakeContext(inputWidth: 32);
        var widthMarker = ctx.GetOrCreateNet("w", NetKind.Bus, 8, NetOrigin.Constant);
        Lower("to_signed", new[] { intIn, widthMarker }, ctx);
        var c = ctx.Build().Cells.SingleOrDefault(x => x.Kind == CellKind.TO_VECTOR);
        return c is not null && c.Attrs!["Signed"] == "true";
    }

    private static bool Test_Resize()
    {
        var (ctx, src) = MakeContext(inputWidth: 4);
        var newWidth = ctx.GetOrCreateNet("nw", NetKind.Bus, 16, NetOrigin.Constant);
        Lower("resize", new[] { src, newWidth }, ctx);
        var c = ctx.Build().Cells.SingleOrDefault(x => x.Kind == CellKind.RESIZE);
        return c is not null
            && c.Attrs!["FromWidth"] == "4"
            && c.Attrs["ToWidth"]   == "16";
    }

    private static bool Test_ShiftLeft()
    {
        var (ctx, src) = MakeContext(inputWidth: 8);
        var n = ctx.GetOrCreateNet("n", NetKind.Wire, 1, NetOrigin.Constant);
        Lower("shift_left", new[] { src, n }, ctx);
        return ctx.Build().Cells.Any(c => c.Kind == CellKind.SHL);
    }

    private static bool Test_ShiftRight()
    {
        var (ctx, src) = MakeContext(inputWidth: 8);
        var n = ctx.GetOrCreateNet("n", NetKind.Wire, 1, NetOrigin.Constant);
        Lower("shift_right", new[] { src, n }, ctx);
        return ctx.Build().Cells.Any(c => c.Kind == CellKind.SHR);
    }

    private static bool Test_RotateLeft()
    {
        var (ctx, src) = MakeContext(inputWidth: 8);
        var n = ctx.GetOrCreateNet("n", NetKind.Wire, 1, NetOrigin.Constant);
        Lower("rotate_left", new[] { src, n }, ctx);
        return ctx.Build().Cells.Any(c => c.Kind == CellKind.ROL);
    }

    private static bool Test_RotateRight()
    {
        var (ctx, src) = MakeContext(inputWidth: 8);
        var n = ctx.GetOrCreateNet("n", NetKind.Wire, 1, NetOrigin.Constant);
        Lower("rotate_right", new[] { src, n }, ctx);
        return ctx.Build().Cells.Any(c => c.Kind == CellKind.ROR);
    }

    private static bool Test_TypeCasts()
    {
        foreach (var fn in new[] { "unsigned", "signed", "std_logic_vector" })
        {
            var (ctx, src) = MakeContext(inputWidth: 8);
            Lower(fn, new[] { src }, ctx);
            var c = ctx.Build().Cells.SingleOrDefault(x => x.Kind == CellKind.BUF);
            if (c is null || c.Attrs is null || c.Attrs["Cast"] != fn)
                return false;
        }
        return true;
    }

    private static bool Test_TryLowerUnknown()
    {
        var ctx = new SynthesisContext("test");
        var arg = ctx.GetOrCreateNet("a", NetKind.Wire, 1, NetOrigin.PortIn);
        return IEEEPrimitives.TryLower("not_a_function",
            new IEEEPrimitiveArgs(new[] { arg }, ctx, SourceLine: null)) is null;
    }

    private static bool Test_OutputWidths()
    {
        // to_integer always returns 32-bit; type-casts preserve width;
        // resize uses the requested new width.
        var (ctx1, b1) = MakeContext(8);
        var r1 = Lower("to_integer", new[] { b1 }, ctx1);
        if (r1.OutputNet.Width != 32) return false;

        var (ctx2, b2) = MakeContext(8);
        var r2 = Lower("unsigned", new[] { b2 }, ctx2);
        if (r2.OutputNet.Width != 8) return false;

        var (ctx3, b3) = MakeContext(4);
        var nw = ctx3.GetOrCreateNet("nw", NetKind.Bus, 16, NetOrigin.Constant);
        var r3 = Lower("resize", new[] { b3, nw }, ctx3);
        if (r3.OutputNet.Width != 16) return false;

        return true;
    }

    private static bool Test_AllNamesIncludesCore()
    {
        var names = IEEEPrimitives.AllRegisteredNames();
        string[] required =
        {
            "rising_edge", "falling_edge",
            "to_integer", "to_unsigned", "to_signed",
            "resize",
            "shift_left", "shift_right", "rotate_left", "rotate_right",
            "unsigned", "signed", "std_logic_vector",
            "conv_integer", "conv_std_logic_vector"
        };
        foreach (var r in required)
            if (!names.Contains(r, StringComparer.OrdinalIgnoreCase))
                return false;
        return true;
    }
}
