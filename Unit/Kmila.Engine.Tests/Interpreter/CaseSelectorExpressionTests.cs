using Interpreter.Models;
using Interpreter.Services;
using Parser.Models;

namespace Interpreter.Tests;

/// <summary>
/// Regression tests for case statements whose selector is an EXPRESSION
/// (function call / type conversion / arithmetic) rather than a plain signal —
/// e.g. <c>case to_integer(cnt_q) is</c>. Before the 2026-10-11 fix the engine
/// resolved the selector only as a single <see cref="Variable"/>, so an
/// expression selector left <c>Selector == null</c> and the whole case silently
/// did nothing (not even <c>when others</c>) — a 7-segment decoder driven this
/// way stayed frozen at its reset value while the counter advanced.
///
/// These cases drive a real counter+decoder design through the live
/// <see cref="DeltaCycleEngine"/> (the same path the Visual/headless runtime
/// uses) and assert the output actually tracks the count. The decode table and
/// expected patterns are cross-checked against GHDL (reference simulator).
/// </summary>
public static class CaseSelectorExpressionTests
{
    public static async Task<int> RunAllTests()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Case Selector Expression Tests");
        Console.WriteLine("========================================");

        var cases = new (string Name, Func<Task<bool>> Run)[]
        {
            ("case to_integer(unsigned cnt) decodes 7-seg", TestToIntegerUnsignedSelector),
            ("case to_integer(unsigned(slv)) nested cast",  TestToIntegerNestedCastSelector),
            ("regression: case <signal> is (plain)",        TestPlainSignalSelectorStillWorks),
            ("regression: case <integer signal> is",        TestIntegerSignalSelectorStillWorks),
        };

        int failed = 0;
        foreach (var (name, run) in cases)
        {
            Console.WriteLine($"\n=== Test: {name} ===");
            bool ok;
            try { ok = await run(); }
            catch (Exception ex)
            {
                Console.WriteLine($"  EXCEPTION {ex.GetType().Name}: {ex.Message}");
                ok = false;
            }
            Console.WriteLine(ok ? "PASS" : "FAIL");
            if (!ok) failed++;
        }
        Console.WriteLine($"\nCase Selector Expression Tests: {cases.Length - failed}/{cases.Length} passed");
        return failed;
    }

    // Decode table shared by the decoder designs below (common anode, abcdefg,
    // a = bit6 … g = bit0; '0' lights a segment). Matches the shipped samples.
    private const string Seg0 = "0000001", Seg1 = "1001111", Seg2 = "0010010", Seg3 = "0000110";

    private static string Design(string decoderBody) => $@"
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;
entity dut is
  port (
    clk : in STD_LOGIC;
    reset : in STD_LOGIC;
    run : in STD_LOGIC;
    count : out STD_LOGIC_VECTOR(3 downto 0);
    segments : out STD_LOGIC_VECTOR(6 downto 0)
  );
end entity dut;
architecture behavioral of dut is
  signal cnt_q : unsigned(3 downto 0) := (others => '0');
  signal numero : integer range 0 to 15 := 0;
  signal code : std_logic_vector(3 downto 0) := (others => '0');
begin
  process(clk, reset)
  begin
    if reset = '1' then cnt_q <= (others => '0');
    elsif rising_edge(clk) then if run = '1' then cnt_q <= cnt_q + 1; end if; end if;
  end process;
  count  <= std_logic_vector(cnt_q);
  numero <= to_integer(cnt_q);
  code   <= std_logic_vector(cnt_q);
{decoderBody}
end architecture behavioral;";

    private static async Task<bool> TestToIntegerUnsignedSelector()
    {
        string body = $@"
  process(cnt_q)
  begin
    case to_integer(cnt_q) is
      when 0 => segments <= ""{Seg0}"";
      when 1 => segments <= ""{Seg1}"";
      when 2 => segments <= ""{Seg2}"";
      when 3 => segments <= ""{Seg3}"";
      when others => segments <= ""1111111"";
    end case;
  end process;";
        var seen = await RunAndCollectSegmentsAsync(Design(body));
        return AssertDecodes(seen);
    }

    private static async Task<bool> TestToIntegerNestedCastSelector()
    {
        string body = $@"
  process(code)
  begin
    case to_integer(unsigned(code)) is
      when 0 => segments <= ""{Seg0}"";
      when 1 => segments <= ""{Seg1}"";
      when 2 => segments <= ""{Seg2}"";
      when 3 => segments <= ""{Seg3}"";
      when others => segments <= ""1111111"";
    end case;
  end process;";
        var seen = await RunAndCollectSegmentsAsync(Design(body));
        return AssertDecodes(seen);
    }

    private static async Task<bool> TestPlainSignalSelectorStillWorks()
    {
        string body = $@"
  process(cnt_q)
  begin
    case cnt_q is
      when ""0000"" => segments <= ""{Seg0}"";
      when ""0001"" => segments <= ""{Seg1}"";
      when ""0010"" => segments <= ""{Seg2}"";
      when ""0011"" => segments <= ""{Seg3}"";
      when others => segments <= ""1111111"";
    end case;
  end process;";
        var seen = await RunAndCollectSegmentsAsync(Design(body));
        return AssertDecodes(seen);
    }

    private static async Task<bool> TestIntegerSignalSelectorStillWorks()
    {
        string body = $@"
  process(numero)
  begin
    case numero is
      when 0 => segments <= ""{Seg0}"";
      when 1 => segments <= ""{Seg1}"";
      when 2 => segments <= ""{Seg2}"";
      when 3 => segments <= ""{Seg3}"";
      when others => segments <= ""1111111"";
    end case;
  end process;";
        var seen = await RunAndCollectSegmentsAsync(Design(body));
        return AssertDecodes(seen);
    }

    // Each decoder must produce the four distinct decoded patterns as the
    // counter walks 0→3 — i.e. the display tracks the count instead of freezing.
    private static bool AssertDecodes(HashSet<string> seen)
    {
        bool ok = seen.Contains(Seg0) && seen.Contains(Seg1) && seen.Contains(Seg2) && seen.Contains(Seg3);
        Console.WriteLine($"  distinct segments = [{string.Join(", ", seen)}]  decodes0-3={ok}");
        return ok;
    }

    private static async Task<HashSet<string>> RunAndCollectSegmentsAsync(string vhdl)
    {
        var (entities, _) = Program.ProcessVhdlCode(vhdl);
        var entity = entities.FirstOrDefault(e => e.Architecture != null)
                     ?? throw new InvalidOperationException("No entity with architecture parsed");

        var allSignals = new HashSet<Variable>(entity.Ports.Concat(entity.Architecture!.Signals));
        InitialValueSeeder.Apply(InitialValuePolicy.Didactic, InitialValueSeeder.ObjectsOf(entity), new HashSet<Variable>());
        // run=1, reset=0 so the counter free-runs.
        foreach (var p in entity.Ports)
        {
            if (p.Name.Equals("run", StringComparison.OrdinalIgnoreCase)) p.Value = "1";
            if (p.Name.Equals("reset", StringComparison.OrdinalIgnoreCase)) p.Value = "0";
        }

        var clk = entity.Ports.FirstOrDefault(p => p.Name.Equals("clk", StringComparison.OrdinalIgnoreCase));
        var segments = entity.Ports.FirstOrDefault(p => p.Name.Equals("segments", StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException("no segments port");

        using var engine = new DeltaCycleEngine();
        engine.Initialize(allSignals, entity.Architecture.Tasks);

        var seen = new HashSet<string>();

        // Settle combinational logic at the reset state (cnt_q = 0) and sample
        // BEFORE the first clock edge so the decode of 0 is captured too.
        engine.AdvanceTime(0);
        await engine.RunTimeStepAsync();
        var initial = segments.Value?.ToString();
        if (!string.IsNullOrEmpty(initial)) seen.Add(initial);

        for (int cycle = 0; cycle < 12; cycle++)
        {
            if (clk is not null)
            {
                clk.Value = "1"; engine.AdvanceTime((ulong)(cycle * 2 + 1)); await engine.RunTimeStepAsync();
                clk.Value = "0"; engine.AdvanceTime((ulong)(cycle * 2 + 2)); await engine.RunTimeStepAsync();
            }
            else { engine.AdvanceTime((ulong)(cycle + 1)); await engine.RunTimeStepAsync(); }
            var v = segments.Value?.ToString();
            if (!string.IsNullOrEmpty(v)) seen.Add(v);
        }
        return seen;
    }
}
