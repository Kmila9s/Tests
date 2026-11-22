using Interpreter.Services;

namespace Interpreter.Tests;

/// <summary>
/// Regression coverage for <see cref="IEEELibraryLoader"/>. Verifies that
/// the loader populates <see cref="FunctionRegistry"/> with the canonical
/// IEEE function names — both via the .vhd stub files when available and
/// via the in-process fallback list when not — and that
/// <see cref="FunctionRegistry.Clear"/> preserves IEEE seeds across
/// per-fixture resets.
/// </summary>
public static class IEEELibraryLoaderTests
{
    /// <summary>Runs the suite. Returns 0 on full pass, otherwise the failed-test count.</summary>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== IEEE Library Loader Tests ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("Loader is idempotent",                                Test_Idempotent),
            ("std_logic_1164 names are registered",                 Test_StdLogic1164Names),
            ("numeric_std names are registered",                    Test_NumericStdNames),
            ("PackageName is recorded on every IEEE entry",         Test_PackageNameTagged),
            ("FunctionRegistry.Clear preserves IEEE seeds",         Test_ClearPreservesSeeds),
            ("FunctionRegistry.ClearAll wipes everything",          Test_ClearAllResets),
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

    private static bool Test_Idempotent()
    {
        IEEELibraryLoader.Instance.EnsureLoaded();
        bool wasLoaded = IEEELibraryLoader.Instance.IsLoaded;
        IEEELibraryLoader.Instance.EnsureLoaded(); // second call must be a no-op
        return wasLoaded && IEEELibraryLoader.Instance.IsLoaded;
    }

    private static bool Test_StdLogic1164Names()
    {
        IEEELibraryLoader.Instance.EnsureLoaded();
        string[] expected = { "rising_edge", "falling_edge", "to_x01", "is_x" };
        foreach (var n in expected)
            if (!FunctionRegistry.Instance.IsKnown(n))
                return false;
        return true;
    }

    private static bool Test_NumericStdNames()
    {
        IEEELibraryLoader.Instance.EnsureLoaded();
        string[] expected =
        {
            "to_integer", "to_unsigned", "to_signed", "resize",
            "shift_left", "shift_right", "rotate_left", "rotate_right"
        };
        foreach (var n in expected)
            if (!FunctionRegistry.Instance.IsKnown(n))
                return false;
        return true;
    }

    private static bool Test_PackageNameTagged()
    {
        IEEELibraryLoader.Instance.EnsureLoaded();
        var def = FunctionRegistry.Instance.Get("rising_edge");
        if (def == null) return false;
        return !string.IsNullOrEmpty(def.PackageName);
    }

    private static bool Test_ClearPreservesSeeds()
    {
        IEEELibraryLoader.Instance.EnsureLoaded();
        // Add a user-declared (PackageName = null) entry, then clear.
        FunctionRegistry.Instance.Register(new Models.FunctionDefinition
        {
            Name        = "user_func_xyz",
            PackageName = null
        });
        if (!FunctionRegistry.Instance.IsKnown("user_func_xyz")) return false;
        FunctionRegistry.Instance.Clear();
        // User entry is gone, IEEE seeds remain.
        return !FunctionRegistry.Instance.IsKnown("user_func_xyz")
            && FunctionRegistry.Instance.IsKnown("to_integer");
    }

    private static bool Test_ClearAllResets()
    {
        IEEELibraryLoader.Instance.EnsureLoaded();
        FunctionRegistry.Instance.ClearAll();
        if (FunctionRegistry.Instance.IsKnown("to_integer")) return false;
        // Restore for downstream tests.
        IEEELibraryLoader.Instance.Reset();
        IEEELibraryLoader.Instance.EnsureLoaded();
        return FunctionRegistry.Instance.IsKnown("to_integer");
    }
}
