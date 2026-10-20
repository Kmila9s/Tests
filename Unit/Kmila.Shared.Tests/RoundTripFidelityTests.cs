using System.IO;
using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Round-trip fidelity audit (2026-07-06). Runs each fixture VHDL through
//
//   V0 --(builder.Build)--> blocks0 --(emitter.Emit)--> V1
//   V1 --(builder.Build)--> blocks1 --(emitter.Emit)--> V2
//
// Three assertions per fixture:
//   • V1 == V2       byte-idempotent (starting from Blocks or from VHDL,
//                    the emitted VHDL stays the same across re-round-trips)
//   • LastRawCount == <baseline>  locks today's raw count; future F-* work
//                    can only reduce it, never grow it
//   • kitchen-sink   also asserts per-construct raw block counts so a
//                    regression on one gap can't hide behind a fix on
//                    another
//
// Fixtures live under Fixtures/RoundTripFidelity/*.vhd — the 10 Example
// gallery samples (mirrored verbatim from ProjectsView.razor at audit
// time) + one purpose-built kitchen_sink.vhd.
public class RoundTripFidelityTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "RoundTripFidelity");

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(FixtureDir, name + ".vhd");
        return File.ReadAllText(path);
    }

    // Runs the 4-step pipeline, returning (V1, V2, LastRawCount after V0 build).
    private static (string V1, string V2, int RawCount) Roundtrip(string V0)
    {
        var b1 = new VhdlBlockBuilder(logger: null);
        var e1 = new VhdlBlockEmitter(logger: null);
        var blocks0 = b1.Build(V0);
        var rawCount = b1.LastRawCount;
        var V1 = e1.Emit(blocks0);
        var b2 = new VhdlBlockBuilder(logger: null);
        var e2 = new VhdlBlockEmitter(logger: null);
        var blocks1 = b2.Build(V1);
        var V2 = e2.Emit(blocks1);
        return (V1, V2, rawCount);
    }

    // Byte-idempotence + raw-count baseline in one shared helper. Any
    // divergence surfaces as a rich failure message including a diff hint
    // so the audit reader can classify the failure quickly.
    private static void AssertRoundTrip(string fixtureName, int expectedRawCount)
    {
        var V0 = LoadFixture(fixtureName);
        var (V1, V2, rawCount) = Roundtrip(V0);

        if (V1 != V2)
        {
            // Dump both to /tmp so a manual `diff` can classify the drift.
            var dir = "/tmp/kmila_roundtrip_audit";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{fixtureName}.V0.vhd"), V0);
            File.WriteAllText(Path.Combine(dir, $"{fixtureName}.V1.vhd"), V1);
            File.WriteAllText(Path.Combine(dir, $"{fixtureName}.V2.vhd"), V2);
            var b = new VhdlBlockBuilder(logger: null);
            var json = b.Build(V0);
            File.WriteAllText(Path.Combine(dir, $"{fixtureName}.blocks.json"), json);
        }
        Assert.True(V1 == V2,
            $"{fixtureName}: byte-idempotence failed (V1 != V2). " +
            $"raws={rawCount}. Diff: /tmp/kmila_roundtrip_audit/{fixtureName}.V[12].vhd");

        Assert.True(rawCount == expectedRawCount,
            $"{fixtureName}: raw count changed from baseline. " +
            $"Expected {expectedRawCount}, got {rawCount}. " +
            "If a fix reduced it, update the baseline. If it grew, that's a regression.");
    }

    // ── Example gallery — 10 fixtures ─────────────────────────────────
    // Baselines recorded 2026-07-06 against the state after F-8 + F-9
    // completeness shipped. Update via the test failure message when a
    // future F-* ships and legitimately reduces a count.

    // Baselines recorded 2026-07-06 against post-audit state. The `when
    // …else` conditional concurrent assign and the `unsigned` /
    // `std_logic_vector(x)` type conversions are the biggest sources of
    // residual raws on the Demo Board / Traffic Light / Buzzer / RGB
    // Rainbow samples (documented in BLOCK_EDITOR_ROUNDTRIP_2026-07-06.md).
    // Every raw survives verbatim so the emitted VHDL still compiles;
    // they just don't render as first-class blocks in the canvas.
    // 2026-07-19 Phase 9 — comment chain blocks are now first-class in
    // all four slots (top/decl/concurrent/seq). The banner comments that
    // Phase 8 surfaced as raw_top and the inline comments across process
    // bodies are all captured now. Every real gallery fixture is CLEAN
    // (0 raws) except buzzer_scale which still has one residual (a
    // sequential loop or wait shape not yet in the primitives — future
    // work). This is the target 1:1 state.
    [Fact] public void RT_SampleMain_DemoBoard()      => AssertRoundTrip("sample_main",     expectedRawCount: 0);
    [Fact] public void RT_CounterClock()              => AssertRoundTrip("counter_clock",   expectedRawCount: 0);
    [Fact] public void RT_TrafficLight_FSM()          => AssertRoundTrip("traffic_light",   expectedRawCount: 0);
    [Fact] public void RT_ShiftRegister()             => AssertRoundTrip("shift_register",  expectedRawCount: 0);
    [Fact] public void RT_DipAdder()                  => AssertRoundTrip("dip_adder",       expectedRawCount: 0);
    [Fact] public void RT_MatrixPlus()                => AssertRoundTrip("matrix_plus",     expectedRawCount: 0);
    // 2026-07-19 Phase 5 — paren-strip in BuildExpression drops the 3
    // parenthesized-`(not X)` raws that were surviving on the RGB
    // derivation. Baseline 3 → 0 (clean round-trip).
    [Fact] public void RT_RgbRainbow()                => AssertRoundTrip("rgb_rainbow",     expectedRawCount: 0);
    // 2026-07-19 Phase 20c — baseline 1 → 0. The lone residual raw was
    // `to_unsigned(N, W)`-style multi-arg calls being misclassified as
    // scalar-index expressions with `<idx>` containing a comma. IndexRe
    // now excludes commas from idx so multi-arg calls fall through to
    // CallHeadRe.
    [Fact] public void RT_BuzzerScale()               => AssertRoundTrip("buzzer_scale",    expectedRawCount: 0);
    [Fact] public void RT_KeypadDisplay()             => AssertRoundTrip("keypad_display",  expectedRawCount: 0);
    [Fact] public void RT_LcdHello()                  => AssertRoundTrip("lcd_hello",       expectedRawCount: 0);
    // 2026-07-19 Phase 19 — synthetic subprograms fixture (component_decl
    // + procedure_decl + function_decl + procedure_call in one file).
    // Shared with the Playwright verify-vhdl-blocks-1to1 spec.
    [Fact] public void RT_P19_Subprograms_Fixture()   => AssertRoundTrip("p19_subprograms", expectedRawCount: 0);
    // 2026-07-23 Phase 3 roundtrip follow-up — procedure declared INSIDE
    // a process declarative region. Mirrors SimulatorBench case 144.
    // Pre-fix would produce 3 raws (raw_decl for the procedure header
    // + 2 raw_seq for `end` / `procedure;`); post-fix must be 0.
    [Fact] public void RT_ProcessWithProcedure()      => AssertRoundTrip("process_with_procedure", expectedRawCount: 0);

    // ── Kitchen sink — synthetic hostile fixture ──────────────────────
    // Designed to fail loudly. Every raw is expected; every open F-*
    // ticket is exercised here. Byte-idempotence NOW asserted after the
    // 2026-07-06 batch 2 fix (scan/original alignment bug in TryParseIf
    // — comments before the first statement of a branch were being
    // Trim()-desynchronized). The audit's original F-Nc "case-subject
    // with parens" diagnosis was a misread: `case sig(hi downto lo) is`
    // parses correctly in isolation (see RT_MicroCase_ParenSubject
    // regression fence) — the observed corruption was the alignment bug
    // firing inside the deeply-nested case's parent-if body.
    // 2026-07-19 1:1 audit:
    //   • Phase 1 (std_ulogic bit-lit dropdown 'X'/'Z'/'U'/'-'/'L'/'H'/'W')
    //     removed one 'X' raw from the case_others arm.
    //   • Phase 4 (F-11 labeled process + component instantiation) removed
    //     one labeled-process raw and one component-inst raw.
    //   • Phase 5 (F-7 for/if-generate + AssignConcurrent target widened
    //     to accept indexed LHS) removed the for-generate raw.
    //   • Phase 8 (top-level library/use) preserved header comment as raw.
    //   • Phase 9 (comment chain blocks) converted every raw comment into
    //     a first-class kmila_vhdl_comment_* block. **KITCHEN SINK IS
    //     NOW BYTE-IDEMPOTENT AT 0 RAWS** — first time in the block
    //     editor's history. Every construct is first-class.
    // Total: 13 → 0.
    [Fact] public void RT_KitchenSink()               => AssertRoundTrip("kitchen_sink",    expectedRawCount: 0);

    // ── Regression fences (2026-07-06 batch 2) ────────────────────────
    // Two hand-crafted micros that hard-code the two bug conditions the
    // batch closed. RT_MicroCase_ParenSubject asserts the audit's F-Nc
    // misdiagnosis stays a non-issue — case with a paren-subject
    // round-trips clean. RT_MicroKitchenInnerIf asserts the scan/original
    // alignment fix: comments before statements inside doubly-nested if
    // bodies survive verbatim without truncation or duplication.

    [Fact]
    public void RT_CondAssign_ChainedWhenElse()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is
  port (
    a : in std_logic;
    b : in std_logic;
    y : out std_logic
  );
end entity;
architecture rtl of t is
begin
  y <= '1' when a = '1' else '0' when b = '1' else 'X';
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Contains("y <= '1' when a = '1' else '0' when b = '1' else 'X';", V1);
        // 2026-07-19 1:1 audit Phase 1: 'X' is now a first-class bit-literal
        // (BitLiteralRe accepts the full std_ulogic alphabet). ZERO raws.
        Assert.Equal(0, raws);
    }

    [Fact]
    public void RT_Call_MultiArg_And_Nested()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity t is
  port (
    a : in std_logic_vector(3 downto 0);
    b : in std_logic_vector(3 downto 0);
    s : out std_logic_vector(4 downto 0)
  );
end entity;
architecture rtl of t is
begin
  s <= std_logic_vector(resize(unsigned(a), 5) + resize(unsigned(b), 5));
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("std_logic_vector(resize(unsigned(a), 5) + resize(unsigned(b), 5));", V1);
    }

    [Fact]
    public void RT_TypeEnum_TrafficLight()
    {
        var V0 = LoadFixture("traffic_light");
        var (V1, V2, _) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Contains("type state_t is (S_RED, S_GREEN, S_YELLOW);", V1);
        Assert.Contains("signal st : state_t := S_RED;", V1);
    }

    [Fact]
    public void RT_F18_SignalInit_Preserved()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity t is port(y : out std_logic_vector(7 downto 0)); end entity;
architecture rtl of t is
  signal r : std_logic_vector(7 downto 0) := (others => '0');
  signal a, b : std_logic := '1';
  signal ucnt : unsigned(3 downto 0) := ""0000"";
begin
  y <= r;
end architecture;
";
        var (V1, V2, _) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Contains(":= (others => '0')", V1);
        Assert.Contains("signal a : std_logic := '1';", V1);
        Assert.Contains("signal b : std_logic := '1';", V1);
        Assert.Contains("ucnt : unsigned(3 downto 0) := \"0000\"", V1);
    }

    [Fact]
    public void RT_MicroCase_ParenSubject()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is port(sig : in std_logic_vector(7 downto 0); y : out std_logic); end entity;
architecture rtl of t is
begin
  process(sig)
  begin
    case sig(1 downto 0) is
      when ""00"" => y <= '0';
      when ""01"" => y <= '1';
      when others => y <= 'X';
    end case;
  end process;
end architecture;
";
        var (V1, V2, _) = Roundtrip(V0);
        // Byte-idempotence is what this fence proves — the audit's F-Nc
        // diagnosis (case-subject with parens is buggy) was a misread; the
        // parser handles `case sig(hi downto lo) is` correctly. The one
        // residual raw is the `'X'` bit-literal in `when others =>`, which
        // falls to raw_expr because BitLitRe only accepts '0' | '1' — a
        // separate expression-parser gap, not a case-parser bug.
        Assert.Equal(V1, V2);
        Assert.Contains("case sig(1 downto 0) is", V1);
        Assert.Contains("when \"00\" =>", V1);
        Assert.Contains("when \"01\" =>", V1);
        Assert.Contains("when others =>", V1);
    }

    [Fact]
    public void RT_MicroKitchenInnerIf()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity t is port(clk : in std_logic; en : in std_logic; cnt_in : in std_logic_vector(7 downto 0); y : out std_logic); end entity;
architecture rtl of t is
  signal r : std_logic_vector(7 downto 0);
  signal ucnt : unsigned(7 downto 0);
begin
  process(clk)
  begin
    if rising_edge(clk) then
      if en = '1' then
        -- Arithmetic with integer literal (F-9 clean).
        ucnt <= ucnt + 1;
        -- Concatenation (F-9 clean).
        r <= r(6 downto 0) & cnt_in(0);
        -- Case with vector-literal choices (F-4 clean).
        case cnt_in(1 downto 0) is
          when ""00""   => y <= '0';
          when ""01""   => y <= '1';
          when others => y <= 'X';
        end case;
      end if;
    end if;
  end process;
end architecture;
";
        var (V1, V2, _) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        // Comments must survive verbatim (previously silently dropped
        // when they preceded the first statement of an if-body, or
        // truncated into fragments like "h integer literal" when they
        // sat between statements). Full-string containment implicitly
        // rules out both failure modes.
        Assert.Contains("-- Arithmetic with integer literal (F-9 clean).", V1);
        Assert.Contains("-- Concatenation (F-9 clean).", V1);
        Assert.Contains("-- Case with vector-literal choices (F-4 clean).", V1);
    }

    // ── 2026-07-19 1:1 audit — regression fences per phase ───────────

    // Phase 1: full std_ulogic bit-lit alphabet.
    [Fact]
    public void RT_P1_StdUlogic_AllNineLiterals()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is
  port (
    s : in  std_logic_vector(2 downto 0);
    y : out std_logic
  );
end entity;
architecture rtl of t is
begin
  process (s) begin
    case s is
      when ""000"" => y <= '0';
      when ""001"" => y <= '1';
      when ""010"" => y <= 'X';
      when ""011"" => y <= 'Z';
      when ""100"" => y <= 'U';
      when ""101"" => y <= 'L';
      when ""110"" => y <= 'H';
      when ""111"" => y <= 'W';
      when others => y <= '-';
    end case;
  end process;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("y <= 'X';", V1);
        Assert.Contains("y <= 'Z';", V1);
        Assert.Contains("y <= '-';", V1);
    }

    // Phase 2: sequential primitives — every keyword becomes a first-class block.
    [Fact]
    public void RT_P2_SequentialPrimitives_AllSeven()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity tb is end entity;
architecture bhv of tb is
begin
  process
  begin
    report ""starting"" severity note;
    assert 1 = 1 report ""ok"" severity note;
    wait for 10 ns;
    wait until 1 = 1;
    null;
    return;
    wait;
  end process;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("report", V1);
        Assert.Contains("assert", V1);
        Assert.Contains("wait for", V1);
        Assert.Contains("wait until", V1);
        Assert.Contains("null;", V1);
        Assert.Contains("return;", V1);
    }

    // Phase 3: loops — for-loop, while-loop, bare loop.
    [Fact]
    public void RT_P3_Loops_ForWhileBare()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity tb is end entity;
architecture bhv of tb is
begin
  process
  begin
    for i in 0 to 3 loop
      null;
    end loop;
    while 1 = 1 loop
      exit when 1 = 1;
    end loop;
    loop
      exit;
    end loop;
    wait;
  end process;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("for i in 0 to 3 loop", V1);
        Assert.Contains("while", V1);
    }

    // Phase 4 (F-11): labeled processes + component instantiation.
    [Fact]
    public void RT_P4_LabeledProcess_And_ComponentInst()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity top is
  port (
    clk : in std_logic;
    y   : out std_logic
  );
end entity;
architecture rtl of top is
  signal q : std_logic;
begin
  u1: counter port map (a => clk, b => q);
  p_out: process (q) begin
    y <= q;
  end process p_out;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("u1: counter", V1);
        Assert.Contains("p_out: process", V1);
        Assert.Contains("end process p_out;", V1);
    }

    // Phase 5 (F-7): for-generate + if-generate + indexed-LHS + paren-strip.
    [Fact]
    public void RT_P5_Generate_ForAndIf_WithIndexedLhs()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity fanout is
  port (
    r : in  std_logic_vector(7 downto 0);
    q : out std_logic_vector(7 downto 0)
  );
end entity;
architecture rtl of fanout is
begin
  gen: for i in 0 to 7 generate
    q(i) <= (not r(i));
  end generate gen;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("gen: for i in 0 to 7 generate", V1);
        Assert.Contains("q(i) <=", V1);
    }

    // Phase 6: with … select.
    [Fact]
    public void RT_P6_WithSelect_Mux4()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity mux4 is
  port (
    s : in std_logic_vector(1 downto 0);
    a, b, c, d : in std_logic;
    y : out std_logic
  );
end entity;
architecture rtl of mux4 is
begin
  with s select y <= a when ""00"", b when ""01"", c when ""10"", d when others;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("with s select", V1);
        Assert.Contains("when others", V1);
    }

    // Phase 7: Tier D declarations — every kind at once.
    [Fact]
    public void RT_P7_TierD_AllDeclKinds()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is end entity;
architecture rtl of t is
  constant N : integer := 8;
  subtype small_t is integer range 0 to 3;
  type mem_t is array (0 to 3) of std_logic_vector(7 downto 0);
  type bus_t is record
    addr : std_logic_vector(7 downto 0);
    data : std_logic_vector(7 downto 0);
  end record;
  alias a_lo is N;
  attribute keep : boolean;
  attribute keep of a_lo : signal is true;
begin
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("constant N : integer := 8", V1);
        Assert.Contains("subtype small_t is integer range 0 to 3", V1);
        Assert.Contains("type mem_t is array", V1);
        Assert.Contains("type bus_t is record", V1);
        Assert.Contains("alias a_lo is N", V1);
        Assert.Contains("attribute keep", V1);
    }

    // Phase 8: top-level library/use + generics + package/package_body.
    [Fact]
    public void RT_P8_TopLevel_Library_Use_Generics_Package()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

package my_pkg is
  constant PI : integer := 3;
end package my_pkg;

entity paramcnt is
  generic (
    WIDTH : integer := 8
  );
  port (
    clk : in std_logic;
    y   : out std_logic
  );
end entity;
architecture rtl of paramcnt is
begin
  y <= clk;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("library ieee;", V1);
        Assert.Contains("use ieee.numeric_std.all;", V1);
        Assert.Contains("package my_pkg is", V1);
        Assert.Contains("generic (", V1);
        Assert.Contains("WIDTH : integer := 8", V1);
    }

    // Phase 9: comments in every slot survive as first-class blocks.
    [Fact]
    public void RT_P9_Comments_InEverySlot()
    {
        const string V0 = @"-- top-of-file banner
library ieee;
use ieee.std_logic_1164.all;
-- between top-level nodes
entity t is
  port ( clk : in std_logic; y : out std_logic );
end entity;
architecture rtl of t is
  -- decl-slot comment
  signal q : std_logic := '0';
begin
  -- concurrent-slot comment
  y <= q;
  process (clk) begin
    -- seq-slot comment
    if rising_edge(clk) then
      q <= not q;
    end if;
  end process;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("-- top-of-file banner", V1);
        Assert.Contains("-- between top-level nodes", V1);
        Assert.Contains("-- decl-slot comment", V1);
        Assert.Contains("-- concurrent-slot comment", V1);
        Assert.Contains("-- seq-slot comment", V1);
    }

    // Phase 10: positional + named aggregates.
    [Fact]
    public void RT_P10_Aggregates_PositionalAndNamed()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is
  port (
    a : out std_logic_vector(3 downto 0);
    b : out std_logic_vector(31 downto 0)
  );
end entity;
architecture rtl of t is
begin
  process begin
    a <= (0 => '1', 1 => '0', 2 => '1', 3 => '0');
    b <= (0 => '1', 4 => '1', others => '0');
    wait;
  end process;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("0 => '1'", V1);
        Assert.Contains("others => '0'", V1);
    }

    // Phase 11: multi-name port declaration (single line → multiple ports).
    [Fact]
    public void RT_P11_MultiNamePorts()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is
  port (
    a, b, c : in  std_logic;
    y, z    : out std_logic
  );
end entity;
architecture rtl of t is
begin
  y <= a;
  z <= b;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        // Each name becomes its own port block on emit — no synthetic
        // fallback with the raw text as NAME (that was the data-corruption
        // bug pre-Phase 11).
        Assert.Contains("a : in std_logic", V1);
        Assert.Contains("b : in std_logic", V1);
        Assert.Contains("c : in std_logic", V1);
        Assert.Contains("y : out std_logic", V1);
        Assert.Contains("z : out std_logic", V1);
    }

    // Phase 12 (F-17): `not (a and b)` retains parens on emit.
    [Fact]
    public void RT_P12_NotOverBinop_KeepsParens()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity t is
  port ( a, b : in std_logic; y : out std_logic );
end entity;
architecture rtl of t is
begin
  y <= not (a and b);
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        // Without Phase 12, the emitter would produce `not a and b` which
        // reparses as `(not a) and b` — wrong precedence.
        Assert.Contains("not (a and b)", V1);
    }

    // Phase 0: emitter silent-loss diagnostics — a hand-built workspace
    // with a missing OP on a binop emits a visible /* MISSING OP */
    // marker AND records a diagnostic entry (not a silent LOGIC-FLIP).
    [Fact]
    public void RT_P0_SilentLoss_Binop_MissingOp_EmitsMarker()
    {
        const string workspace = """
            {"blocks":{"blocks":[{"type":"kmila_vhdl_entity","fields":{"NAME":"t"},"inputs":{"PORTS":{"block":{"type":"kmila_vhdl_port","fields":{"NAME":"y","DIR":"out","TYPE":"std_logic"}}}}},{"type":"kmila_vhdl_architecture","fields":{"NAME":"rtl","OF":"t"},"inputs":{"BODY":{"block":{"type":"kmila_vhdl_assign_concurrent","fields":{"TARGET":"y"},"inputs":{"VALUE":{"block":{"type":"kmila_vhdl_binop","inputs":{"LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"a"}}},"RHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"b"}}}}}}}}}}}]}}
            """;
        var e = new VhdlBlockEmitter(logger: null);
        var vhdl = e.Emit(workspace);
        Assert.Contains("/* MISSING OP */", vhdl);
        Assert.NotEmpty(e.LastDiagnostics);
        // 2026-07-20 Phase 23a — LastDiagnostics is now List<EmitterDiagnostic>.
        // The old string.Contains predicate migrates to .Message.Contains.
        Assert.Contains(e.LastDiagnostics, d => d.Message.Contains("binop"));
    }

    // 2026-07-20 Phase 23a — silent-loss diagnostics carry a source Line
    // (1-based) so Editor.razor can project them into Monaco markers. The
    // binop-missing-OP workspace above emits its /* MISSING OP */ inside
    // the architecture body — that line is > 0 and less than the total
    // emitted line count. BlockId is best-effort: the JSON above does NOT
    // carry an "id" field on the binop, so BlockId is null here — but
    // real workspaces (produced by kmilaBlocklySerialize) always carry
    // ids, so a fresh workspace WITH an id populates the field.
    [Fact]
    public void RT_P23a_SilentLossDiagnostic_CarriesLineNumber()
    {
        const string workspaceWithId = """
            {"blocks":{"blocks":[{"type":"kmila_vhdl_entity","fields":{"NAME":"t"},"inputs":{"PORTS":{"block":{"type":"kmila_vhdl_port","fields":{"NAME":"y","DIR":"out","TYPE":"std_logic"}}}}},{"type":"kmila_vhdl_architecture","fields":{"NAME":"rtl","OF":"t"},"inputs":{"BODY":{"block":{"type":"kmila_vhdl_assign_concurrent","fields":{"TARGET":"y"},"inputs":{"VALUE":{"block":{"type":"kmila_vhdl_binop","id":"bin_broken_1","inputs":{"LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"a"}}},"RHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"b"}}}}}}}}}}}]}}
            """;
        var e = new VhdlBlockEmitter(logger: null);
        var vhdl = e.Emit(workspaceWithId);
        Assert.NotEmpty(e.LastDiagnostics);
        var diag = e.LastDiagnostics.First(d => d.Message.Contains("binop"));
        Assert.True(diag.Line > 0, $"Diagnostic Line must be > 0, got {diag.Line}");
        Assert.Equal("bin_broken_1", diag.BlockId);
        Assert.Equal(8, diag.Severity); // Monaco Error
    }

    // ── Phase 19 (2026-07-19) — subprogram declarations + procedure calls ──
    // Component declarations in architecture DECLS, procedure declarations
    // with local variables + assert/report/wait bodies, function declarations
    // with return-type + attribute-using bodies, and procedure calls used as
    // sequential statements inside testbench stimulus.

    // Component declaration alone — the shape testbenches use to declare
    // a UUT before instantiating it below.
    [Fact]
    public void RT_P19_ComponentDecl_TestbenchUut()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity tb is
end entity;
architecture sim of tb is
    component xor_gate is
        port (
            a : in std_logic;
            b : in std_logic;
            y : out std_logic
        );
    end component;
    signal a, b, y : std_logic;
begin
    u1: xor_gate port map (a => a, b => b, y => y);
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("component xor_gate is", V1);
        Assert.Contains("end component;", V1);
    }

    // Procedure declaration with a multi-argument signature and a body
    // containing wait + assert + report + severity — the exact shape
    // testbench check_and_gate uses across the 001/002/… gate cases.
    [Fact]
    public void RT_P19_ProcedureDecl_WithAssertReport()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity tb is
end entity;
architecture sim of tb is
    signal y : std_logic;
    procedure check_and_gate(
        a_in : in std_logic;
        b_in : in std_logic;
        expected_y : in std_logic
    ) is
    begin
        wait for 1 ns;
        assert (y = expected_y) report ""ERROR"" severity failure;
    end procedure;
begin
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("procedure check_and_gate", V1);
        Assert.Contains("end procedure;", V1);
        Assert.Contains("wait for 1 ns;", V1);
    }

    // Function declaration — same envelope shape as procedure_decl but
    // with the extra `return <TYPE>` clause. Mirrors 034-hamming's
    // sum_bits helper.
    [Fact]
    public void RT_P19_FunctionDecl_WithLocalVarAndLoop()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity dummy is
end entity;
architecture rtl of dummy is
    function sum_bits(s : in std_logic_vector) return integer is
        variable acc : integer := 0;
    begin
        return acc;
    end function;
begin
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("function sum_bits", V1);
        Assert.Contains("return integer", V1);
        Assert.Contains("end function;", V1);
    }

    // Procedure calls as sequential statements — the primary raw_seq
    // source across every testbench in the corpus. Guard rail: multiple
    // args, single-arg-string form, no-args form must all round-trip
    // clean without collapsing to raw_seq.
    [Fact]
    public void RT_P19_ProcedureCall_InProcessBody()
    {
        const string V0 = @"library ieee;
use ieee.std_logic_1164.all;
entity tb is
end entity;
architecture sim of tb is
    procedure printmsg(msg : in string) is
    begin
        report msg;
    end procedure;
    procedure check_and_gate(
        a_in : in std_logic;
        b_in : in std_logic;
        expected_y : in std_logic
    ) is
    begin
        wait for 1 ns;
    end procedure;
begin
    stim: process
    begin
        printmsg(""Start"");
        check_and_gate('0', '0', '0');
        check_and_gate('1', '1', '1');
        wait;
    end process;
end architecture;
";
        var (V1, V2, raws) = Roundtrip(V0);
        Assert.Equal(V1, V2);
        Assert.Equal(0, raws);
        Assert.Contains("printmsg(\"Start\");", V1);
        Assert.Contains("check_and_gate('0', '0', '0');", V1);
        Assert.Contains("check_and_gate('1', '1', '1');", V1);
    }
}
