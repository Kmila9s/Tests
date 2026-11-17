using Interpreter.Models;
using Interpreter.Repositories;
using Interpreter.Services;
using Parser.Models;
using Parser.Repositories;
using static Parser.Models.Constants;

namespace Kmila.Shared.Tests;

// Phase 3 (Session 26, 2026-07-22) — parse-side + shape pins for the
// procedure-body-wait rewrite. Full engine-level behaviour is covered
// by SimulatorBench cases 144-procedure-wait + 145-procedure-if-branch
// (timed transitions match GHDL byte-for-byte). These xUnit tests lock
// the AST-surface contract so a regression in the synth path or the
// frame model can't silently degrade to the pre-Phase-3 silent-drop
// behaviour.
public class ProcedureFrameTests
{
    private static TokenStream StreamFrom(string source)
    {
        var tokenizer = new Tokenizer();
        tokenizer.ReadString(source);
        tokenizer.Evaluate();
        return new TokenStream(tokenizer.GetDictionaryType());
    }

    // ProcedureFrame is a sealed IProcessFrame with init-only Tasks /
    // CallRef / BoundScope and a mutable InnerPc. Locks the
    // wiring the delta-engine drain switch relies on.
    [Fact]
    public void ProcedureFrame_ExposesTasksInnerPcBoundScope()
    {
        var tasks = new List<object>();
        var vars = new HashSet<Variable>();
        var call = new ProcedureCallStatement(vars, "noop", new List<string>());
        var frame = new ProcedureFrame
        {
            Tasks = tasks,
            InnerPc = 5,
            CallRef = call,
            BoundScope = vars,
        };

        Assert.Same(tasks, frame.Tasks);
        Assert.Equal(5, frame.InnerPc);
        Assert.Same(call, frame.CallRef);
        Assert.Same(vars, frame.BoundScope);
        Assert.IsAssignableFrom<IProcessFrame>(frame);

        frame.InnerPc = 7;
        Assert.Equal(7, frame.InnerPc);
    }

    // FunctionExecutor.ParseFunctionBodyStatic MUST capture a bare
    // `wait for N ns;` as a WaitForStatement task. Pre-Phase-3 the
    // default arm in the KEYWORDS switch swallowed `wait`, so every
    // procedure-body wait vanished silently. This is the load-bearing
    // parse-side pin for Phase 3.
    [Fact]
    public void ParseFunctionBodyStatic_CapturesTopLevelWait()
    {
        var stream = StreamFrom("wait for 15 ns ; report \"done\" ;");
        var tasks = FunctionExecutor.ParseFunctionBodyStatic(stream, new HashSet<Variable>());

        Assert.Contains(tasks, t => t is WaitForStatement);
        var wait = tasks.OfType<WaitForStatement>().First();
        Assert.True(wait.IsTimed);
        Assert.Equal(15, wait.DelayNs);
    }

    // A wait nested inside an if branch in the procedure body must also
    // survive the parse — this is what Phase 3 case 145 exercises at
    // engine level. Parse-side pin: the branch's Tasks contain the wait.
    [Fact]
    public void ParseFunctionBodyStatic_CapturesWaitInsideIfBranch()
    {
        var s = new Variable("s", "0", DATATYPES.STD_LOGIC);
        var vars = new HashSet<Variable> { s };
        var stream = StreamFrom(
            "if s = '0' then wait for 20 ns ; s <= '1' ; else wait for 10 ns ; s <= '0' ; end if ;");
        var tasks = FunctionExecutor.ParseFunctionBodyStatic(stream, vars);

        var ifs = tasks.OfType<IfStructure>().FirstOrDefault();
        Assert.NotNull(ifs);
        Assert.Contains(ifs.Branches, b => b.Tasks.OfType<WaitForStatement>().Any());
    }

    // report / assert dispatch pin: both must show up as their own
    // ISControl instances after the Phase 3 dispatch repair.
    [Fact]
    public void ParseFunctionBodyStatic_DispatchesReportAndAssert()
    {
        var stream = StreamFrom("report \"hi\" ; assert true report \"ok\" severity note ;");
        var tasks = FunctionExecutor.ParseFunctionBodyStatic(stream, new HashSet<Variable>());

        Assert.Contains(tasks, t => t is ReportStatement);
        Assert.Contains(tasks, t => t is AssertStatement);
    }

    // ResolveBoundBody performs formal→actual textual substitution and
    // parses the result against a caller-facing scope. When formal `s`
    // is bound to caller-side signal `state`, the resulting Operation's
    // assignment target Variable identity must be `state` (not a fresh
    // shadow bound under name `s`). This is Option C's core correctness
    // argument.
    [Fact]
    public void ResolveBoundBody_RenamesFormalToActualIdentity()
    {
        var state = new Variable("state", "0", DATATYPES.STD_LOGIC);
        var callerScope = new HashSet<Variable> { state };

        // Register a minimal procedure in FunctionRegistry.
        var def = new FunctionDefinition
        {
            Name = "toggle",
            IsPure = false,
            BodyRaw = "s <= '1' ;",
            Parameters = new List<ParameterDefinition>
            {
                new ParameterDefinition
                {
                    Name = "s",
                    Type = DATATYPES.STD_LOGIC,
                    Mode = ParameterMode.InOut,
                    IsSignal = true,
                },
            },
        };
        FunctionRegistry.Instance.Register(def);

        try
        {
            var call = new ProcedureCallStatement(
                callerScope, "toggle", new List<string> { "state" });

            var tasks = call.ResolveBoundBody(
                scheduler: null, out var boundScope);

            Assert.NotEmpty(tasks);
            var op = tasks.OfType<Operation>().FirstOrDefault();
            Assert.NotNull(op);
            // The rebuilt Operation's assignment target must be the
            // caller-side `state` Variable, not a shadow bound under `s`.
            Assert.Equal("state", op.Result?.Name, ignoreCase: true);
            Assert.Same(state, op.Result);
        }
        finally
        {
            // Don't leak the test-only registration into other tests.
            FunctionRegistry.Instance.Clear();
        }
    }

    // ResolveBoundBody's cache MUST return the same List<object>
    // instance on repeated resolution with the same actual signature,
    // so per-call parse cost is amortized to one for testbench-shape
    // procedures called many times.
    [Fact]
    public void ResolveBoundBody_CachesByActualNameSignature()
    {
        var state = new Variable("state", "0", DATATYPES.STD_LOGIC);
        var callerScope = new HashSet<Variable> { state };

        var def = new FunctionDefinition
        {
            Name = "toggle_cached",
            IsPure = false,
            BodyRaw = "s <= '1' ;",
            Parameters = new List<ParameterDefinition>
            {
                new ParameterDefinition
                {
                    Name = "s",
                    Type = DATATYPES.STD_LOGIC,
                    Mode = ParameterMode.InOut,
                    IsSignal = true,
                },
            },
        };
        FunctionRegistry.Instance.Register(def);

        try
        {
            var call1 = new ProcedureCallStatement(
                callerScope, "toggle_cached", new List<string> { "state" });
            var call2 = new ProcedureCallStatement(
                callerScope, "toggle_cached", new List<string> { "state" });

            var tasks1 = call1.ResolveBoundBody(null, out _);
            var tasks2 = call2.ResolveBoundBody(null, out _);

            Assert.Same(tasks1, tasks2);
        }
        finally
        {
            FunctionRegistry.Instance.Clear();
        }
    }

    // Session 29 (2026-07-23) — literal actual (no caller-scope Variable
    // match) must not NRE when the `!= null` overload trap on Variable
    // returns TRUE for a null reference. Prior to the fix, calling
    // `step(1)` on `procedure step(constant N : in integer)` NRE'd in
    // ResolveBoundBody because `actualVar != null` for a null actualVar
    // hit the overloaded operator (Names comparison), branched into the
    // if arm, and NRE'd on `actualVar.Name`. Fix: use `is not null`.
    // Load-bearing for SimulatorBench case 146-procedure-time-formal.
    [Fact]
    public void ResolveBoundBody_LiteralActual_NoNullOverloadNRE()
    {
        var callerScope = new HashSet<Variable>(); // no caller variable named "1"

        var def = new FunctionDefinition
        {
            Name = "step_lit",
            IsPure = false,
            BodyRaw = "wait for 20 ns ;",
            Parameters = new List<ParameterDefinition>
            {
                new ParameterDefinition
                {
                    Name = "N",
                    Type = DATATYPES.INTEGER,
                    Mode = ParameterMode.In,
                },
            },
        };
        FunctionRegistry.Instance.Register(def);

        try
        {
            var call = new ProcedureCallStatement(
                callerScope, "step_lit", new List<string> { "1" });

            // Must not NRE. Prior to the fix this threw
            // NullReferenceException at actualVar.Name.
            var tasks = call.ResolveBoundBody(
                scheduler: null, out var boundScope);

            Assert.NotEmpty(tasks);
        }
        finally
        {
            FunctionRegistry.Instance.Clear();
        }
    }
}
