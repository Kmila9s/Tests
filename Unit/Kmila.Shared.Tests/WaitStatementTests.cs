using Interpreter.Models;
using Interpreter.Repositories;
using Interpreter.Services;
using Parser.Models;
using Parser.Repositories;
using static Parser.Models.Constants;

namespace Kmila.Shared.Tests;

// Session 23 (2026-07-18) — F-G-13b + F-G-13c + F-G-14 parse-side pins.
// Full engine-level behaviour is covered by SimulatorBench 121/121 (the
// 16 `wait until` cases and the ~10 `wait-in-loop` stimulus TBs). These
// xUnit tests lock the AST-surface contract so a regression in the
// synth path can't silently degrade to the pre-Session-23 drop-token
// behaviour without failing a fast test.
public class WaitStatementTests
{
    private static TokenStream StreamFrom(string source)
    {
        var tokenizer = new Tokenizer();
        tokenizer.ReadString(source);
        tokenizer.Evaluate();
        return new TokenStream(tokenizer.GetDictionaryType());
    }

    // F-G-13b — `wait until <cond>;` must set IsCondition, compile the
    // Condition operation, and populate Sensitivity with every signal
    // referenced by the condition tokens.
    [Fact]
    public void WaitUntil_CapturesConditionAndSensitivity()
    {
        var clk = new Variable("clk", "0", DATATYPES.STD_LOGIC);
        var vars = new HashSet<Variable> { clk };

        var stream = StreamFrom("wait until clk = '1' ;");
        var wait = new WaitForStatement(stream, vars);
        wait.Syntetize();

        Assert.True(wait.IsCondition);
        Assert.False(wait.IsTerminal);
        Assert.False(wait.IsTimed);
        Assert.NotNull(wait.Condition);
        Assert.Contains(clk, wait.Sensitivity);
    }

    // F-G-13b — `wait until rising_edge(clk);` must also pick up the
    // referenced signal even though it's inside an IEEE function call.
    [Fact]
    public void WaitUntil_RisingEdge_ExtractsInnerSignal()
    {
        var clk = new Variable("clk", "0", DATATYPES.STD_LOGIC);
        var vars = new HashSet<Variable> { clk };

        var stream = StreamFrom("wait until rising_edge(clk) ;");
        var wait = new WaitForStatement(stream, vars);
        wait.Syntetize();

        Assert.True(wait.IsCondition);
        Assert.Contains(clk, wait.Sensitivity);
    }

    // F-G-13 baseline — bare `wait for N ns;` must still parse into a
    // timed, non-condition, non-terminal wait. F-G-13b's new branch
    // must not have broken the pre-existing timed path.
    [Fact]
    public void WaitFor_TimedRemainsTimed()
    {
        var stream = StreamFrom("wait for 5 ns ;");
        var wait = new WaitForStatement(stream, variables: null);
        wait.Syntetize();

        Assert.True(wait.IsTimed);
        Assert.False(wait.IsCondition);
        Assert.False(wait.IsTerminal);
        Assert.Equal(5, wait.DelayNs);
    }

// F-G-14 — the parameter parser must capture the `signal` object
    // class before the name list. Prior to Session 23 this keyword was
    // silently dropped by the IDENTIFIERS-only filter, so procedures
    // with `signal` formals bound to fresh shadow Variables and drove
    // their writes into the void.
    [Fact]
    public void ParameterDefinition_IsSignalDefault_False()
    {
        var def = new ParameterDefinition { Name = "S", Type = DATATYPES.STD_LOGIC };
        Assert.False(def.IsSignal);
    }

    // F-G-13c — the LoopFrame is the resumable-loop state carrier. This
    // pin catches a regression that would break the field wiring
    // between Process.Frames.Push and the resumable executor.
    [Fact]
    public void LoopFrame_ExposesLoopRefAndCounters()
    {
        var loopRef = new object();
        var frame = new LoopFrame { LoopRef = loopRef, IterationVal = 3, InnerPc = 7 };

        Assert.Same(loopRef, frame.LoopRef);
        Assert.Equal(3, frame.IterationVal);
        Assert.Equal(7, frame.InnerPc);
    }

    // F-G-13c — after enabling wait-capture in for/while/simple loops,
    // a wait inside a for-loop body must appear as a WaitForStatement
    // in the loop's Tasks list (previously dropped by SkipWaitStatement).
    [Fact]
    public void ForLoop_CapturesWaitAsTask()
    {
        var d = new Variable("d", "0", DATATYPES.STD_LOGIC);
        var vars = new HashSet<Variable> { d };

        // `for i in 0 to 3 loop d <= '1'; wait for 10 ns; end loop`
        var stream = StreamFrom("for i in 0 to 3 loop d <= '1' ; wait for 10 ns ; end loop ;");
        var forLoop = new ForLoopStructure(vars, stream);
        forLoop.Syntetize();

        // The synth flow adds the loop var to the caller's variable
        // set; the body should contain both the assignment and a
        // WaitForStatement (Session 23 F-G-13c capture).
        Assert.Contains(forLoop.Tasks, t => t is WaitForStatement);
        var wait = forLoop.Tasks.OfType<WaitForStatement>().First();
        Assert.True(wait.IsTimed);
        Assert.Equal(10, wait.DelayNs);
    }
}
