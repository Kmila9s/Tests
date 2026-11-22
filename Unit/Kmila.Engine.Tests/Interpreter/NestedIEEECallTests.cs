using Interpreter.Models;
using Interpreter.Services;
using Interpreter.Synthesis;
using Parser.Models;
using static Parser.Models.Constants;

namespace Interpreter.Tests;

/// <summary>
/// Regression coverage for #29 — nested IEEE calls in arguments.
///
/// Two angles are checked:
///   1. PARSER + RUNTIME — building an Operation from raw tokens that
///      contain a nested call (e.g. "to_integer ( unsigned ( addr ) )")
///      must produce a DeferredFunction whose Arguments contain the inner
///      DeferredFunction (not the bare string "unsigned"), and the runtime
///      DeferredFunction.Evaluate() must produce the correct integer value.
///   2. SYNTH — the same DeferredFunction tree, fed to the assignment
///      synthesizer, must emit a cell graph where the outer cell's input
///      pin is wired to the inner cell's output net (proper wiring, not
///      a placeholder constant).
/// </summary>
public static class NestedIEEECallTests
{
    /// <summary>Runs all nested IEEE call tests and returns the number that failed.</summary>
    /// <returns>The count of failed tests.</returns>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== Nested IEEE Call Tests (#29) ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("Parser: to_integer(unsigned(addr)) carries nested DF",  Test_ParserNestsToIntegerUnsigned),
            ("Runtime: to_integer(unsigned('01010101')) == 85",       Test_RuntimeToIntegerUnsigned),
            ("Runtime: to_integer(unsigned('00000000')) == 0",        Test_RuntimeZero),
            ("Runtime: to_integer(unsigned('11111111')) == 255",      Test_RuntimeMax),
            ("Synth: nested call produces wired cell graph",          Test_SynthNestedWiring),
            ("Synth: triple-nested resize(unsigned(addr), 16)",       Test_SynthTripleNested),
            ("ShouldDefer: type-name overloads now deferred",         Test_TypeNamesDeferred),
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
    /// Builds a real Operation from a raw VHDL expression and returns the
    /// parsed item list. We need a non-null result variable so the parser
    /// has somewhere to assign — pass any signal-shaped Variable.
    /// </summary>
    /// <summary>
    /// Builds a real Operation from an assignment-shaped raw expression
    /// (the only form Operation's constructor parses) and returns its
    /// item list. The LHS just has to be a Variable already in scope —
    /// the test is about the RHS shape.
    /// </summary>
    private static object[] ParseAssignment(string assignment, HashSet<Variable> scope)
        => new Operation(assignment, scope).Items;

    private static Variable MkSig(string name, string value, int width = 8)
        => new Variable(name, value, DATATYPES.STD_LOGIC_VECTOR, width)
        {
            IsPort = true,
            PortType = PortType.IN,
        };

    // ---- Parser + runtime ----------------------------------------------

    private static bool Test_ParserNestsToIntegerUnsigned()
    {
        var addr  = MkSig("addr", "01010101", width: 8);
        var idx   = new Variable("idx", 0, DATATYPES.INTEGER) { PortType = PortType.OUT };
        var scope = new HashSet<Variable> { addr, idx };
        var items = ParseAssignment("idx <= to_integer ( unsigned ( addr ) )", scope);

        // We expect exactly one top-level DeferredFunction (to_integer)
        // carrying a single argument, which is itself a DeferredFunction
        // for unsigned.
        var top = items.OfType<DeferredFunction>().FirstOrDefault();
        if (top is null || !top.FunctionName.Equals("to_integer", StringComparison.OrdinalIgnoreCase))
            return false;
        if (top.Arguments.Length != 1) return false;
        if (top.Arguments[0] is not DeferredFunction inner) return false;
        return inner.FunctionName.Equals("unsigned", StringComparison.OrdinalIgnoreCase)
            && inner.Arguments.Length == 1
            && inner.Arguments[0] is Variable v
            && v.Name.Equals("addr", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Test_RuntimeToIntegerUnsigned()
    {
        return RuntimeEvalNested("01010101") == 85;
    }

    private static bool Test_RuntimeZero()
    {
        return RuntimeEvalNested("00000000") == 0;
    }

    private static bool Test_RuntimeMax()
    {
        return RuntimeEvalNested("11111111") == 255;
    }

    /// <summary>
    /// Drives a real to_integer(unsigned(addr)) expression through the
    /// parser, evaluates it via Operation.Execute, and returns the
    /// computed integer. Pre-#29 this always returned 0 because the
    /// parser dropped the addr operand.
    /// </summary>
    private static int RuntimeEvalNested(string addrBits)
    {
        var addr  = MkSig("addr", addrBits, width: addrBits.Length);
        var idx   = new Variable("idx", 0, DATATYPES.INTEGER) { PortType = PortType.OUT };
        var scope = new HashSet<Variable> { addr, idx };
        var op = new Operation("idx <= to_integer ( unsigned ( addr ) )", scope);
        var executed = (Variable)op.Execute();
        if (executed.Value is int i) return i;
        if (int.TryParse(executed.Value?.ToString(), out int parsed)) return parsed;
        return -1;
    }

    // ---- Synth side -----------------------------------------------------

    private static bool Test_SynthNestedWiring()
    {
        // Build the DeferredFunction tree by hand (the parser test above
        // already proves the parser produces it correctly) and check the
        // resulting cell graph topology.
        var addr = MkSig("addr", "01010101");
        var idx  = new Variable("idx", 0, DATATYPES.INTEGER);
        var inner = new DeferredFunction("unsigned",
            new object[] { addr },
            DATATYPES.STD_LOGIC_VECTOR,
            new HashSet<Variable> { addr });
        var outer = new DeferredFunction("to_integer",
            new object[] { inner },
            DATATYPES.INTEGER,
            new HashSet<Variable> { addr });

        var ctx = new SynthesisContext("test_entity");
        ctx.AddPort(addr);
        var outNet = ctx.GetOrCreateNet("idx", NetKind.Bus, 32, NetOrigin.Signal);
        // Carrier text references only declared names; items are injected below.
        var op = new Operation(addr.Name, new HashSet<Variable> { addr, idx });
        InjectItems(op, new object[] { outer });
        ConcurrentAssignSynthesizer.SynthesizeInto(op, outNet, ctx);
        var nl = ctx.Build();

        // Expect both cells: a BUF (the unsigned cast) and a TO_INTEGER
        // (the outer call). The BUF's output net id must match one of the
        // TO_INTEGER's input pins.
        var buf = nl.Cells.FirstOrDefault(c => c.Kind == CellKind.BUF
                                            && c.Attrs is not null
                                            && c.Attrs.TryGetValue("Cast", out var cast)
                                            && cast == "unsigned");
        var toInt = nl.Cells.FirstOrDefault(c => c.Kind == CellKind.TO_INTEGER);
        if (buf is null || toInt is null) return false;
        var bufOutId  = buf.Outputs[0].NetId;
        var toIntInId = toInt.Inputs[0].NetId;
        return bufOutId == toIntInId;
    }

    private static bool Test_SynthTripleNested()
    {
        // resize(unsigned(addr), 16) — three levels:
        //   resize ( unsigned ( addr ) , 16 )
        // Cell graph: BUF (cast) → RESIZE
        var addr = MkSig("addr", "0101", width: 4);
        var widen= new Variable("widen", 0, DATATYPES.STD_LOGIC_VECTOR, 16);

        var inner = new DeferredFunction("unsigned",
            new object[] { addr },
            DATATYPES.STD_LOGIC_VECTOR,
            new HashSet<Variable> { addr });
        var widthNet = new Variable("wdummy", 16, DATATYPES.INTEGER);
        var outer = new DeferredFunction("resize",
            new object[] { inner, 16 },
            DATATYPES.STD_LOGIC_VECTOR,
            new HashSet<Variable> { addr });

        var ctx = new SynthesisContext("test_entity");
        ctx.AddPort(addr);
        var outNet = ctx.GetOrCreateNet("widen", NetKind.Bus, 16, NetOrigin.Signal);
        var op = new Operation(addr.Name, new HashSet<Variable> { addr });
        InjectItems(op, new object[] { outer });
        ConcurrentAssignSynthesizer.SynthesizeInto(op, outNet, ctx);
        var nl = ctx.Build();

        bool hasBuf    = nl.Cells.Any(c => c.Kind == CellKind.BUF
                                        && c.Attrs is not null
                                        && c.Attrs.TryGetValue("Cast", out var v)
                                        && v == "unsigned");
        bool hasResize = nl.Cells.Any(c => c.Kind == CellKind.RESIZE);
        return hasBuf && hasResize;
    }

    // ---- ShouldDefer policy --------------------------------------------

    private static bool Test_TypeNamesDeferred()
    {
        return DeferredFunction.ShouldDefer("unsigned")
            && DeferredFunction.ShouldDefer("signed")
            && DeferredFunction.ShouldDefer("std_logic_vector")
            && DeferredFunction.ShouldDefer("conv_std_logic_vector");
    }

    // ---- Reflection helper for injecting items -------------------------

    private static void InjectItems(Operation op, object[] items)
    {
        var t = typeof(Operation);
        var field = t.GetField($"<{nameof(Operation.Items)}>k__BackingField",
                               System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field is null)
            throw new InvalidOperationException("Couldn't find Items backing field");
        field.SetValue(op, items);
    }
}
