using Interpreter.Services;
using Parser.Models;

namespace Interpreter.Tests;

/// <summary>
/// Regression coverage for the typo-in-port-type strictness in
/// <see cref="VariableSyntetizer"/>. Mirrors the negative fixtures the
/// standalone Parser module runs from its <c>Program.cs</c>, but exercises
/// the path the App actually takes (Entity.Syntetize → VariableSyntetizer),
/// which previously silently swallowed unknown port types and let
/// <c>STDLOGIC</c> / <c>STD_LGIC</c> through.
/// </summary>
public static class PortStrictnessTests
{
    /// <summary>
    /// Runs every port-strictness test in sequence, printing pass/fail for each.
    /// </summary>
    /// <returns>The number of tests that failed.</returns>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== Port Strictness Tests ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("STDLOGIC (missing underscore) is rejected with suggestion", Test_StdLogicTypo),
            ("STD_LGIC (missing letter) is rejected with suggestion",     Test_StdLgicTypo),
            ("Real user-defined types still pass through",                Test_UserDefinedTypePasses),
            ("Diagnostic column lands on the typo, not on whitespace",    Test_DiagnosticColumnIsCharOffset),
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

    private static bool Test_StdLogicTypo() => ExpectThrowsWithSuggestion(
        EntityLines("foo", "clk : in STDLOGIC"),
        expectedHint: "std_logic");

    private static bool Test_StdLgicTypo() => ExpectThrowsWithSuggestion(
        EntityLines("foo", "clk : in STD_LGIC"),
        expectedHint: "std_logic");

    private static bool Test_UserDefinedTypePasses()
    {
        // A type name that's far from any canonical name (Levenshtein > 2)
        // must pass through unchallenged — strictness is for typos, not for
        // rejecting custom types the simulator can't yet model.
        try
        {
            var entity = new Entity();
            entity.Syntetize(EntityLines("foo", "data : in my_custom_bus_t"));
            return true; // parsed without throwing — desired behaviour
        }
        catch (ParserException) { return false; }
    }

    private static bool Test_DiagnosticColumnIsCharOffset()
    {
        // Mimics the user's repro: a leading-indented port-list line where
        // STDLOGIC is the 4th token but sits at character column 18+.
        // Without the source-aware overload, Diagnostic would point at
        // character column 4 (whitespace) and the Monaco squiggle would
        // appear on "bit2" instead of "STDLOGIC".
        const string source =
            "entity foo is\n" +
            "  port (\n" +
            "    bit2   : out STDLOGIC\n" +
            "  );\n" +
            "end foo;";

        try
        {
            var lines = source.Split('\n');
            var entityLines = new List<(int sourceLine, string text)>();
            for (int i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length == 0) continue;
                entityLines.Add((i, t));
            }

            var e = new Entity();
            e.Syntetize(entityLines);
            return false; // expected to throw
        }
        catch (ParserException ex)
        {
            var diag = Diagnostic.FromException(ex, source);
            // Line "    bit2   : out STDLOGIC": STDLOGIC starts at character
            // index 17 → 1-based column 18.
            const string offending = "    bit2   : out STDLOGIC";
            var expectedStart = offending.IndexOf("STDLOGIC", StringComparison.Ordinal) + 1;
            return diag.StartColumn == expectedStart &&
                   diag.EndColumn == expectedStart + "STDLOGIC".Length;
        }
    }

    private static bool ExpectThrowsWithSuggestion(
        IReadOnlyList<(int sourceLine, string text)> lines,
        string expectedHint)
    {
        try
        {
            var entity = new Entity();
            entity.Syntetize(lines);
        }
        catch (ParserException ex)
        {
            return ex.Message.Contains(expectedHint, StringComparison.OrdinalIgnoreCase) &&
                   ex.Message.Contains("Did you mean", StringComparison.OrdinalIgnoreCase);
        }
        return false; // no throw → strictness gap is back
    }

    private static IReadOnlyList<(int sourceLine, string text)> EntityLines(
        string name, string portDecl)
    {
        return new List<(int, string)>
        {
            (1, $"entity {name} is"),
            (2, "port ("),
            (3, $"  {portDecl}"),
            (4, ");"),
            (5, $"end {name};"),
        };
    }
}
