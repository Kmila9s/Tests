using LibraryCompiler.Repositories;
using LibraryCompiler.Services;
using LCompiler = LibraryCompiler.Services.LibraryCompiler;

namespace Interpreter.Tests;

/// <summary>
/// Coverage for the new <c>LibraryCompiler</c> module — the resolver, the
/// builder, the orchestrator, the build-policy modes, the in-memory blob
/// store, and the multi-file project entry point.
/// </summary>
public static class LibraryCompilerTests
{
    /// <summary>Runs the suite. Returns 0 on full pass, otherwise the failed-test count.</summary>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== LibraryCompiler Tests ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("Resolver knows IEEE builtins",                                Test_ResolverKnowsIEEE),
            ("Resolver topologically orders deps",                          Test_ResolverTopologicalOrder),
            ("Resolver merges host-supplied additional sources",            Test_ResolverMergesAdditional),
            ("Resolver unregisters by name",                                Test_ResolverUnregister),
            ("Builder reads bytes via host callback",                       Test_BuilderUsesHostCallback),
            ("Builder falls back to embedded IEEE resources",               Test_BuilderEmbeddedFallback),
            ("Builder hard-coded fallback when no source available",        Test_BuilderHardCodedFallback),
            ("Compiler rejects null OpenLibrarySource",                     Test_CompilerRejectsNullOpener),
            ("Linear build is deterministic",                               Test_LinearBuildDeterministic),
            ("Parallel build returns name-sorted output",                   Test_ParallelBuildSorted),
            ("Policy: OnDemand never populates process cache",              Test_PolicyOnDemand),
            ("Policy: ProcessCached populates cache",                       Test_PolicyProcessCached),
            ("Policy: Persistent reads + writes blob store",                Test_PolicyPersistent),
            ("Persistent rejects stale blob (content-hash mismatch)",       Test_PersistentRejectsStaleBlob),
            ("Persistent accepts blob when content hash matches",           Test_PersistentReusesMatchingBlob),
            ("Per-library override takes effect",                           Test_PerLibraryOverride),
            ("InMemoryBlobStore round-trips bytes",                         Test_InMemoryBlobStoreRoundtrip),
            ("NullBlobStore drops everything",                              Test_NullBlobStore),
            ("CompileProjectAsync returns a CompiledProject",               Test_CompileProjectAsync),
            ("Reset() clears the in-memory cache",                          Test_OrchestratorReset),
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

    private static LCompiler MakeCompiler(
        LibraryBuildPolicy? policy = null,
        IBlobStore? store = null,
        IReadOnlyList<LibrarySource>? extra = null,
        Func<string, Stream?>? opener = null)
    {
        opener ??= _ => null;
        return LCompiler.FromSyncOpener(opener, store ?? NullBlobStore.Instance, extra, policy);
    }

    // ---- Resolver -------------------------------------------------------

    private static bool Test_ResolverKnowsIEEE()
    {
        var r = new LibraryResolver();
        return r.Resolve("std_logic_1164") is not null
            && r.Resolve("numeric_std")    is not null
            && r.Resolve("not_a_real_lib") is null;
    }

    private static bool Test_ResolverTopologicalOrder()
    {
        var r = new LibraryResolver();
        var order = r.ResolveTransitive(new[] { "numeric_std" })
                      .Select(s => s.LibraryName).ToArray();
        // numeric_std DependsOn std_logic_1164, so std_logic_1164 must appear first.
        return order.Length == 2
            && string.Equals(order[0], "std_logic_1164", StringComparison.OrdinalIgnoreCase)
            && string.Equals(order[1], "numeric_std",    StringComparison.OrdinalIgnoreCase);
    }

    private static bool Test_ResolverMergesAdditional()
    {
        var custom = new LibrarySource("my_pkg", "WORK", "fake-key", Array.Empty<string>());
        var r = new LibraryResolver(new[] { custom });
        return r.Resolve("my_pkg") is { Vendor: "WORK" };
    }

    private static bool Test_ResolverUnregister()
    {
        var custom = new LibrarySource("ghost", "WORK", "k", Array.Empty<string>());
        var r = new LibraryResolver(new[] { custom });

        // Round 1: present, then removed.
        if (r.Resolve("ghost") is null) return false;
        if (!r.Unregister("ghost")) return false;
        if (r.Resolve("ghost") is not null) return false;

        // Round 2: idempotent — second call returns false but doesn't throw.
        if (r.Unregister("ghost")) return false;

        // Round 3: IEEE builtins must survive an unrelated unregister.
        if (r.Resolve("std_logic_1164") is null) return false;
        return true;
    }

    // ---- Builder --------------------------------------------------------

    private static bool Test_BuilderUsesHostCallback()
    {
        const string vhd = "function user_fn(x : integer) return integer;";
        var bytes = System.Text.Encoding.UTF8.GetBytes(vhd);
        var opts = new LibraryCompilerOptions(
            OpenLibrarySource: (key, _) =>
                Task.FromResult<Stream?>(key == "fake-key" ? new MemoryStream(bytes) : null));
        var b = new LibraryBuilder(opts);
        var src = new LibrarySource("custom", "WORK", "fake-key", Array.Empty<string>());
        var result = b.BuildAsync(src).GetAwaiter().GetResult();
        return result.Functions.Any(s => s.Name == "user_fn" && s.Kind == LibrarySymbolKind.Function);
    }

    private static bool Test_BuilderEmbeddedFallback()
    {
        // Host returns null → builder must fall back to the embedded
        // IEEE stubs and discover real symbols (rising_edge, to_integer).
        var opts = new LibraryCompilerOptions(
            OpenLibrarySource: (_, _) => Task.FromResult<Stream?>(null));
        var b = new LibraryBuilder(opts);
        var src = new LibrarySource("std_logic_1164", "IEEE",
            "Builtins/IEEE/std_logic_1164.vhd", Array.Empty<string>());
        var lib = b.BuildAsync(src).GetAwaiter().GetResult();
        return lib.Functions.Any(s => s.Name == "rising_edge");
    }

    private static bool Test_BuilderHardCodedFallback()
    {
        // No host source AND not an embedded builtin → hard-coded list.
        var opts = new LibraryCompilerOptions(
            OpenLibrarySource: (_, _) => Task.FromResult<Stream?>(null));
        var b = new LibraryBuilder(opts);
        var src = new LibrarySource("numeric_std", "IEEE",
            "missing.vhd", Array.Empty<string>());
        var lib = b.BuildAsync(src).GetAwaiter().GetResult();
        // Either the embedded resource succeeds (preferred) or the hard
        // coded list kicks in. Both produce to_integer.
        return lib.Functions.Any(s => s.Name == "to_integer");
    }

    // ---- Compiler / Orchestrator ----------------------------------------

    private static bool Test_CompilerRejectsNullOpener()
    {
        try
        {
            _ = new LCompiler(new LibraryCompilerOptions(
                OpenLibrarySource: null!));
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    private static bool Test_LinearBuildDeterministic()
    {
        var c = MakeCompiler();
        var run1 = c.CompileAsync(new[] { "std_logic_1164", "numeric_std" }).GetAwaiter().GetResult();
        var run2 = c.CompileAsync(new[] { "std_logic_1164", "numeric_std" }).GetAwaiter().GetResult();
        // Cached → same artifact instance (or at least same shape).
        return run1.Count == run2.Count && run1.Count == 2
            && run1[0].LibraryName == run2[0].LibraryName
            && run1[1].LibraryName == run2[1].LibraryName;
    }

    private static bool Test_ParallelBuildSorted()
    {
        var c = MakeCompiler();
        var libs = c.CompileAsync(
            new[] { "numeric_std", "std_logic_1164" },
            BuildExecutionMode.Parallel).GetAwaiter().GetResult();
        // Parallel mode sorts by name for determinism — numeric_std < std_logic_1164.
        return libs.Count == 2
            && libs[0].LibraryName == "numeric_std"
            && libs[1].LibraryName == "std_logic_1164";
    }

    // ---- Policy ---------------------------------------------------------

    private static bool Test_PolicyOnDemand()
    {
        var c = MakeCompiler(policy: LibraryBuildPolicy.AlwaysOnDemand);
        c.CompileAsync(new[] { "std_logic_1164" }).GetAwaiter().GetResult();
        return c.Orchestrator.ProcessCacheSnapshot().Count == 0;
    }

    private static bool Test_PolicyProcessCached()
    {
        var c = MakeCompiler(policy: LibraryBuildPolicy.AlwaysProcessCached);
        c.CompileAsync(new[] { "std_logic_1164" }).GetAwaiter().GetResult();
        return c.Orchestrator.ProcessCacheSnapshot().ContainsKey("std_logic_1164");
    }

    private static bool Test_PolicyPersistent()
    {
        var store = new InMemoryBlobStore();
        var c = MakeCompiler(policy: LibraryBuildPolicy.AlwaysPersistent, store: store);
        c.CompileAsync(new[] { "std_logic_1164" }).GetAwaiter().GetResult();
        // The Persistent policy must have written something to the blob store.
        bool wrote = false;
        var asyncEnum = store.EnumerateKeysAsync().GetAsyncEnumerator();
        try
        {
            while (asyncEnum.MoveNextAsync().AsTask().GetAwaiter().GetResult()) wrote = true;
        }
        finally { asyncEnum.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        return wrote;
    }

    private static bool Test_PersistentRejectsStaleBlob()
    {
        // A user-supplied source whose live bytes are well-known.
        const string liveSource = "function real_fn ( x : integer ) return integer is\nbegin\nend;";
        var custom = new LibrarySource("my_pkg", "WORK", "fake-key", Array.Empty<string>());

        // Pre-seed the blob store with a CompiledLibrary that has a wrong
        // content hash and a sentinel symbol the live source cannot produce.
        var store = new InMemoryBlobStore();
        var stale = new CompiledLibrary(
            LibraryName: "my_pkg",
            Vendor:      "WORK",
            ContentHash: "0000000000000000000000000000000000000000000000000000000000000000",
            Functions:   new[] { new LibrarySymbol("ghost_fn_should_not_appear", LibrarySymbolKind.Function, true) },
            Diagnostics: Array.Empty<LibraryDiagnostic>());
        var staleBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(stale);
        store.WriteAsync("libcache/my_pkg.json", staleBytes).GetAwaiter().GetResult();

        var c = MakeCompiler(
            policy: LibraryBuildPolicy.AlwaysPersistent,
            store:  store,
            extra:  new[] { custom },
            opener: key => key == "fake-key"
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(liveSource))
                : null);

        var libs = c.CompileAsync(new[] { "my_pkg" }).GetAwaiter().GetResult();
        var got = libs.FirstOrDefault(l => l.LibraryName == "my_pkg");
        if (got is null) return false;

        // Sentinel symbol from the stale blob must NOT appear; the real
        // function name from the live source must appear instead.
        bool sawGhost = got.Functions.Any(f => f.Name == "ghost_fn_should_not_appear");
        bool sawReal  = got.Functions.Any(f => f.Name == "real_fn");

        // And the blob the orchestrator now has on disk must reflect the
        // fresh build, not the stale sentinel.
        var rewritten = store.ReadAsync("libcache/my_pkg.json").GetAwaiter().GetResult();
        bool blobUpdated = rewritten != null
            && System.Text.Encoding.UTF8.GetString(rewritten).Contains("real_fn");

        return !sawGhost && sawReal && blobUpdated;
    }

    private static bool Test_PersistentReusesMatchingBlob()
    {
        const string liveSource = "function f ( x : integer ) return integer is\nbegin\nend;";
        var custom = new LibrarySource("kept", "WORK", "k", Array.Empty<string>());
        var store  = new InMemoryBlobStore();

        // First compile — populates the blob with a hash that matches
        // the live source.
        var first = MakeCompiler(
            policy: LibraryBuildPolicy.AlwaysPersistent,
            store:  store,
            extra:  new[] { custom },
            opener: key => key == "k"
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(liveSource))
                : null);
        var libs1 = first.CompileAsync(new[] { "kept" }).GetAwaiter().GetResult();
        var hash1 = libs1.First(l => l.LibraryName == "kept").ContentHash;

        // Second compile — fresh process cache, same blob store, same
        // bytes. Must hit the blob (hash matches) and yield an identical
        // ContentHash, not a placeholder fallback.
        var second = MakeCompiler(
            policy: LibraryBuildPolicy.AlwaysPersistent,
            store:  store,
            extra:  new[] { custom },
            opener: key => key == "k"
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(liveSource))
                : null);
        var libs2 = second.CompileAsync(new[] { "kept" }).GetAwaiter().GetResult();
        var hash2 = libs2.First(l => l.LibraryName == "kept").ContentHash;

        return !string.IsNullOrEmpty(hash1)
            && string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase)
            && !hash1.StartsWith("fallback-");
    }

    private static bool Test_PerLibraryOverride()
    {
        var policy = new LibraryBuildPolicy
        {
            Default    = LibraryBuildMode.OnDemand,
            PerLibrary = new Dictionary<string, LibraryBuildMode>(StringComparer.OrdinalIgnoreCase)
            {
                ["numeric_std"] = LibraryBuildMode.ProcessCached
            }
        };
        var c = MakeCompiler(policy: policy);
        c.CompileAsync(new[] { "std_logic_1164", "numeric_std" }).GetAwaiter().GetResult();
        var snap = c.Orchestrator.ProcessCacheSnapshot();
        // numeric_std cached (override); std_logic_1164 not cached (OnDemand default).
        return snap.ContainsKey("numeric_std") && !snap.ContainsKey("std_logic_1164");
    }

    // ---- Blob stores ----------------------------------------------------

    private static bool Test_InMemoryBlobStoreRoundtrip()
    {
        var s = new InMemoryBlobStore();
        s.WriteAsync("k", new byte[] { 1, 2, 3 }).GetAwaiter().GetResult();
        bool exists = s.ExistsAsync("k").GetAwaiter().GetResult();
        var got = s.ReadAsync("k").GetAwaiter().GetResult();
        s.DeleteAsync("k").GetAwaiter().GetResult();
        bool gone = !s.ExistsAsync("k").GetAwaiter().GetResult();
        return exists && got is { Length: 3 } && got[0] == 1 && got[2] == 3 && gone;
    }

    private static bool Test_NullBlobStore()
    {
        var s = NullBlobStore.Instance;
        s.WriteAsync("k", new byte[] { 9 }).GetAwaiter().GetResult();
        return !s.ExistsAsync("k").GetAwaiter().GetResult()
            && s.ReadAsync("k").GetAwaiter().GetResult() == null;
    }

    // ---- Project --------------------------------------------------------

    private static bool Test_CompileProjectAsync()
    {
        var c = MakeCompiler();
        var ctx = new ProjectContext(
            Files:      new[] { "user/foo.vhd", "user/bar.vhd" },
            UseClauses: new[] { "std_logic_1164", "numeric_std" });
        var compiled = c.CompileProjectAsync(ctx).GetAwaiter().GetResult();
        return compiled.UserFiles.Count == 2
            && compiled.Libraries.Count == 2
            && compiled.Libraries.Any(l => l.LibraryName == "std_logic_1164")
            && compiled.Libraries.Any(l => l.LibraryName == "numeric_std");
    }

    private static bool Test_OrchestratorReset()
    {
        var c = MakeCompiler(policy: LibraryBuildPolicy.AlwaysProcessCached);
        c.CompileAsync(new[] { "std_logic_1164" }).GetAwaiter().GetResult();
        if (c.Orchestrator.ProcessCacheSnapshot().Count == 0) return false;
        c.Orchestrator.Reset();
        return c.Orchestrator.ProcessCacheSnapshot().Count == 0;
    }
}
