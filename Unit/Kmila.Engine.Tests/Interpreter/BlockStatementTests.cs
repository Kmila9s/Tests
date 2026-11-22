using Interpreter.Models;
using Interpreter.Repositories;
using Interpreter.Services;
using Parser.Models;

namespace Interpreter.Tests;

/// <summary>
/// Regression tests for VHDL <c>block</c> statement support in
/// <see cref="Interpreter.Services.Architecture"/>: plain blocks, blocks with
/// local declarations, nested blocks, and — the important one — <em>guarded</em>
/// blocks whose guard dynamically gates a guarded concurrent assignment.
/// Blocks are flattened into the architecture, so each test asserts the block's
/// concurrent statements actually reach <c>Architecture.Tasks</c> and (for the
/// guarded case) that the driver is disconnected while the guard is false.
/// </summary>
public static class BlockStatementTests
{
    /// <summary>Runs all block statement tests and returns the number that failed.</summary>
    /// <returns>The count of failed tests.</returns>
    public static async Task<int> RunAllTests()
    {
        int failed = 0;
        var tests = new (string Name, Func<Task<bool>> Run)[]
        {
            ("Plain block parses and flattens its body",      TestPlainBlock),
            ("Block with local signal declarations",          TestBlockLocalSignals),
            ("Nested blocks parse",                           TestNestedBlocks),
            ("Guarded block gates its assignment dynamically", TestGuardedBlockGating),
            ("BuildGuardedRhs lowering shape",                TestBuildGuardedRhs),
        };

        foreach (var (name, run) in tests)
        {
            Console.WriteLine($"=== Block statement test: {name} ===");
            bool ok;
            try
            {
                ok = await run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                ok = false;
            }
            Console.WriteLine(ok ? "PASS" : "FAIL");
            if (!ok) failed++;
        }

        return failed;
    }

    /// <summary>Parses VHDL and returns the first entity that has an architecture.</summary>
    private static Entity ParseArch(string raw)
    {
        var (entities, _) = Program.ProcessVhdlCode(raw);
        return entities.FirstOrDefault(e => e.Architecture != null)
               ?? throw new InvalidOperationException("No entity with architecture parsed");
    }

    // --- individual cases ---

    private static Task<bool> TestPlainBlock()
    {
        const string vhdl = @"
entity plain_blk is
    port ( a : in std_logic; b : in std_logic; y : out std_logic );
end plain_blk;
architecture rtl of plain_blk is
begin
    blk : block
    begin
        y <= a and b;
    end block blk;
end rtl;";
        var entity = ParseArch(vhdl);
        bool hasTask = entity.Architecture!.Tasks.Count >= 1;
        Console.WriteLine($"  tasks: {entity.Architecture.Tasks.Count}");
        return Task.FromResult(hasTask);
    }

    private static Task<bool> TestBlockLocalSignals()
    {
        const string vhdl = @"
entity local_sig_blk is
    port ( a : in std_logic; y : out std_logic );
end local_sig_blk;
architecture rtl of local_sig_blk is
begin
    blk : block
        signal tmp : std_logic := '0';
    begin
        tmp <= not a;
        y <= tmp;
    end block blk;
end rtl;";
        var entity = ParseArch(vhdl);
        bool hoisted = entity.Architecture!.Signals.Any(s => s.Name.Equals("tmp", StringComparison.OrdinalIgnoreCase));
        bool hasTasks = entity.Architecture.Tasks.Count >= 2;
        Console.WriteLine($"  local 'tmp' hoisted: {hoisted}, tasks: {entity.Architecture.Tasks.Count}");
        return Task.FromResult(hoisted && hasTasks);
    }

    private static Task<bool> TestNestedBlocks()
    {
        const string vhdl = @"
entity nested_blk is
    port ( a : in std_logic; b : in std_logic; y : out std_logic );
end nested_blk;
architecture rtl of nested_blk is
begin
    outer : block
        signal mid : std_logic := '0';
    begin
        mid <= a;
        inner : block
        begin
            y <= mid and b;
        end block inner;
    end block outer;
end rtl;";
        var entity = ParseArch(vhdl);
        bool hasTasks = entity.Architecture!.Tasks.Count >= 2;
        Console.WriteLine($"  tasks: {entity.Architecture.Tasks.Count}");
        return Task.FromResult(hasTasks);
    }

    /// <summary>
    /// The core semantic test: a guarded block drives <c>dout</c> from <c>din</c>
    /// only while the guard <c>g = '1'</c>; when the guard is false the driver is
    /// disconnected and <c>dout</c> no longer tracks <c>din</c>.
    /// </summary>
    private static async Task<bool> TestGuardedBlockGating()
    {
        const string vhdl = @"
entity guarded_dut is
    port (
        din  : in  std_logic_vector(3 downto 0);
        en   : in  std_logic;
        g    : in  std_logic;
        dout : out std_logic_vector(3 downto 0)
    );
end guarded_dut;
architecture rtl of guarded_dut is
begin
    gb : block (g = '1')
    begin
        dout <= guarded din when en = '1' else (others => '0');
    end block gb;
end rtl;";
        var entity = ParseArch(vhdl);
        var arch = entity.Architecture!;

        var allSignals = new HashSet<Variable>(entity.Ports.Concat(arch.Signals));
        Variable Sig(string n) => allSignals.First(s => s.Name.Equals(n, StringComparison.OrdinalIgnoreCase));

        InitialValueSeeder.Apply(InitialValuePolicy.Didactic, InitialValueSeeder.ObjectsOf(entity), new HashSet<Variable>());

        using var engine = new DeltaCycleEngine();
        engine.Initialize(allSignals, arch.Tasks);

        async Task Settle(ulong t) { engine.AdvanceTime(t); await engine.RunTimeStepAsync(); }

        var din = Sig("din"); var en = Sig("en"); var g = Sig("g"); var dout = Sig("dout");

        // Guard TRUE, enable TRUE → dout should track din.
        din.Value = "1010"; en.Value = "1"; g.Value = "1";
        await Settle(1);
        string doutWhenGuarded = dout.Value?.ToString() ?? "";

        // Guard FALSE → driver disconnected → dout must NOT still equal din.
        g.Value = "0";
        await Settle(2);
        string doutWhenBlocked = dout.Value?.ToString() ?? "";

        bool trackedWhenGuarded = doutWhenGuarded == "1010";
        bool disconnectedWhenBlocked = doutWhenBlocked != "1010";
        Console.WriteLine($"  dout(g=1,en=1)='{doutWhenGuarded}' (expect 1010); dout(g=0)='{doutWhenBlocked}' (expect != 1010)");
        return trackedWhenGuarded && disconnectedWhenBlocked;
    }

    private static Task<bool> TestBuildGuardedRhs()
    {
        var block = new BlockStatement("gb") { GuardExpression = "g = '1'" };
        string lowered = block.BuildGuardedRhs("din when en = '1' else ( others => '0' )", "( others => 'Z' )");
        // Must disconnect when the guard is false and otherwise fall through to the
        // original waveform: "<disc> when not ( <guard> ) else <original>".
        bool ok = lowered.Contains("when not ( g = '1' )")
                  && lowered.Contains("( others => 'Z' )")
                  && lowered.TrimEnd().EndsWith("din when en = '1' else ( others => '0' )");
        Console.WriteLine($"  lowered: {lowered}");
        return Task.FromResult(ok);
    }
}
