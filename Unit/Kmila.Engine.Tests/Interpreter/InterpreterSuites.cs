namespace Interpreter.Tests;

/// <summary>
/// The Interpreter's engine suites (v3.0.10: moved out of the shipping assembly,
/// where they ran as <c>Interpreter --test-delta</c>). Each suite prints its own
/// checks and returns its failure count; a suite passes with zero failures, and a
/// failing one reports what it printed.
/// </summary>
public class InterpreterSuites
{
    [Fact] public Task DeltaCycleEngine() => Passes(DeltaCycleEngineTests.RunAllTests);
    [Fact] public Task Synthesis() => Passes(SynthesisTests.RunAllTests);
    [Fact] public Task IEEELibraryLoader() => Passes(IEEELibraryLoaderTests.RunAllTests);
    [Fact] public Task LibraryCompiler() => Passes(LibraryCompilerTests.RunAllTests);
    [Fact] public Task IEEEPrimitives() => Passes(IEEEPrimitivesTests.RunAllTests);
    [Fact] public Task IEEELoweringEndToEnd() => Passes(IEEELoweringEndToEndTests.RunAllTests);
    [Fact] public Task NestedIEEECall() => Passes(NestedIEEECallTests.RunAllTests);
    [Fact] public Task CaseSelectorExpression() => Passes(CaseSelectorExpressionTests.RunAllTests);
    [Fact] public Task PortStrictness() => Passes(PortStrictnessTests.RunAllTests);
    [Fact] public Task SyntaxStrictness() => Passes(SyntaxStrictnessTests.RunAllTests);
    [Fact] public Task BlockStatement() => Passes(BlockStatementTests.RunAllTests);

    private static Task Passes(Func<int> suite) => Passes(() => Task.FromResult(suite()));

    /// <summary>Runs <paramref name="suite"/> with the console captured (the assembly is serial) and asserts zero failures.</summary>
    private static async Task Passes(Func<Task<int>> suite)
    {
        var console = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        int failures;
        try { failures = await suite(); }
        finally { Console.SetOut(console); }
        Assert.True(failures == 0, $"{failures} failure(s):\n{output}");
    }
}
