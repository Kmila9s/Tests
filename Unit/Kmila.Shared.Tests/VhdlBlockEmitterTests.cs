using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Pin the Blocks → VHDL direction. Assertions are mostly substring checks
// against the emitted source: exact whitespace would be brittle and the
// property that matters is "student's block program produces canonical
// VHDL a real simulator can parse."
public class VhdlBlockEmitterTests
{
    private static VhdlBlockEmitter NewEmitter() => new(logger: null);

    // Round-trip through Build → Emit; keeps fixture VHDL close to the
    // assertions and exercises both sides in one shot.
    private static string BuildEmit(string vhdl) => NewEmitter().Emit(new VhdlBlockBuilder().Build(vhdl));

    [Fact]
    public void Emit_NullOrEmpty_ReturnsEmptyString()
    {
        var e = NewEmitter();
        Assert.Equal(string.Empty, e.Emit(null));
        Assert.Equal(string.Empty, e.Emit(""));
        Assert.Equal(string.Empty, e.Emit("   "));
    }

    [Fact]
    public void Emit_MalformedJson_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, NewEmitter().Emit("{ this is not json"));
    }

    [Fact]
    public void Emit_EmptyWorkspace_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, NewEmitter().Emit("""{"blocks":{"blocks":[]}}"""));
    }

    // Every non-empty emit must start with the IEEE library preamble the
    // downstream simulator expects. Regression against accidental removal.
    [Fact]
    public void Emit_NonEmptyProgram_StartsWithIeeePreamble()
    {
        const string src = """
            entity foo is port (a : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a; end rtl;
            """;
        var out1 = BuildEmit(src);
        Assert.StartsWith("library ieee;", out1);
        Assert.Contains("use ieee.std_logic_1164.all;", out1);
    }

    // F-1: vector types must survive Build → Emit.
    [Fact]
    public void Emit_F1_VectorTypeSurvivesRoundTrip()
    {
        const string src = """
            entity foo is port (a : in std_logic_vector(3 downto 0); y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a(0); end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("std_logic_vector", emitted);
    }

    // F-2: binop lands as an infix operator in the emitted assignment.
    [Fact]
    public void Emit_F2_BinaryOp_EmitsInfixOperator()
    {
        const string src = """
            entity foo is port (a : in std_logic; b : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a and b; end rtl;
            """;
        var emitted = BuildEmit(src);
        // The word "and" must appear in the emitted body (not just the
        // "end architecture" trailer). Guard by requiring the assignment
        // arrow immediately preceding it.
        Assert.Matches(new System.Text.RegularExpressions.Regex(@"<=\s*[^;]*\band\b"), emitted);
    }

    // F-4: case emits the case…when…end case shape.
    [Fact]
    public void Emit_F4_Case_EmitsCaseWhenEndCase()
    {
        const string src = """
            entity foo is
              port (sel : in std_logic_vector(1 downto 0); y : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is
            begin
              process (sel) begin
                case sel is
                  when "00" => y <= "0001";
                  when others => y <= "0000";
                end case;
              end process;
            end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("case sel is", emitted);
        Assert.Contains("when others", emitted);
        Assert.Contains("end case", emitted);
    }

    // F-5: vector literal renders back as a quoted bit string.
    [Fact]
    public void Emit_F5_VectorLiteral_EmitsQuotedBitString()
    {
        const string src = """
            entity foo is port (y : out std_logic_vector(3 downto 0)); end foo;
            architecture rtl of foo is begin y <= "1010"; end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("\"1010\"", emitted);
    }

    // F-7: falling_edge renders as falling_edge(<clk>).
    [Fact]
    public void Emit_F7_FallingEdge_EmitsFallingEdgeCall()
    {
        const string src = """
            entity foo is port (clk : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin
              process (clk) begin
                if falling_edge(clk) then y <= '0'; end if;
              end process;
            end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("falling_edge(clk)", emitted);
    }

    // LastBlockLineMap: at least one entry per emit that produces content.
    // Guards the Monaco error-decoration path in the editor.
    [Fact]
    public void Emit_PopulatesLastBlockLineMap()
    {
        const string src = """
            entity foo is port (a : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a; end rtl;
            """;
        var e = NewEmitter();
        var _ = e.Emit(new VhdlBlockBuilder().Build(src));
        Assert.NotEmpty(e.LastBlockLineMap);
    }

    // F-9 (2026-07-05): arithmetic operators emit as infix through the
    // shared binop path.
    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("mod")]
    public void Emit_F9_ArithOp_RendersInfix(string op)
    {
        var src = $"""
            entity foo is
              port (a : in std_logic_vector(3 downto 0); b : in std_logic_vector(3 downto 0);
                    y : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is begin y <= a {op} b; end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Matches(new System.Text.RegularExpressions.Regex($@"<=\s*[^;]*\ba\s+{System.Text.RegularExpressions.Regex.Escape(op)}\s+b\b"), emitted);
    }

    // F-10 (2026-07-05): slice emit renders `sig(hi downto lo)`.
    [Fact]
    public void Emit_F10_Slice_RendersSigParenHiDowntoLo()
    {
        const string src = """
            entity foo is
              port (bus : in std_logic_vector(7 downto 0); y : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is begin y <= bus(3 downto 0); end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("bus(3 downto 0)", emitted);
    }

    // F-10 (2026-07-05): index emit renders `sig(idx)`.
    [Fact]
    public void Emit_F10_Index_RendersSigParenIdx()
    {
        const string src = """
            entity foo is port (bus : in std_logic_vector(7 downto 0); y : out std_logic); end foo;
            architecture rtl of foo is begin y <= bus(0); end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("bus(0)", emitted);
    }

    // F-12 (2026-07-05): `(others => '0')` round-trips as the same VHDL.
    [Fact]
    public void Emit_F12_OthersAggregate_RendersOthersArrow()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; q : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is
              signal r : std_logic_vector(3 downto 0);
            begin
              process (clk) begin
                if rising_edge(clk) then
                  r <= (others => '0');
                end if;
              end process;
              q <= r;
            end rtl;
            """;
        var emitted = BuildEmit(src);
        Assert.Contains("(others => '0')", emitted);
    }

    // F-8 (2026-07-05) — helpers for hand-built if-block workspaces. Kept
    // local to this file because F-8 is the only group that assembles the
    // legacy vs. new elsif shapes directly, without going through the
    // builder.
    // Wraps a hand-built if-block JSON in a minimal entity + architecture +
    // process shell so EmitTopLevel routes into it (top-level only handles
    // entity/architecture/raw_top). Substring assertions on emitted VHDL
    // then read the if-block content verbatim.
    private static string WrapIfInProcess(string ifBlockJson)
    {
        const string template = """
            {"blocks":{"blocks":[
              {"type":"kmila_vhdl_entity","fields":{"NAME":"foo"}},
              {"type":"kmila_vhdl_architecture","fields":{"NAME":"rtl","OF":"foo"},
               "inputs":{
                 "BODY":{"block":{"type":"kmila_vhdl_process","fields":{"SENS":"clk"},
                   "inputs":{
                     "BODY":{"block": __IFBLOCK__ }
                   }}}
               }}
            ]}}
            """;
        return template.Replace("__IFBLOCK__", ifBlockJson);
    }

    private static string LegacyNestedIfWorkspaceJson() =>
        // if a='1' then y<='1'; elsif b='1' then y<='0'; else y<='Z'; end if;
        // Encoded pre-F-8 as `outer-if { ELSE_BODY: nested-if { ELSE_BODY: else-body } }`.
        WrapIfInProcess(
        """
          {"type":"kmila_vhdl_if","fields":{},
           "inputs":{
             "COND":{"block":{"type":"kmila_vhdl_eq","fields":{},
               "inputs":{
                 "LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"a"}}},
                 "RHS":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
             "THEN_BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
               "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
             "ELSE_BODY":{"block":{"type":"kmila_vhdl_if","fields":{},
               "inputs":{
                 "COND":{"block":{"type":"kmila_vhdl_eq","fields":{},
                   "inputs":{
                     "LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"b"}}},
                     "RHS":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
                 "THEN_BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
                   "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"0"}}}}}},
                 "ELSE_BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
                   "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"Z"}}}}}}
               }}}
           }}
        """);

    private static string MixedWorkspaceJson() =>
        // Outer if with a populated ELSIF_CHAIN (one chain-arm on `b='1'`)
        // AND a legacy nested-if in ELSE_BODY (one legacy-arm on `c='1'`)
        // AND a real else. Emit order must be: if / chain-arm / legacy-arm / else.
        WrapIfInProcess(
        """
          {"type":"kmila_vhdl_if","fields":{},
           "inputs":{
             "COND":{"block":{"type":"kmila_vhdl_eq","fields":{},
               "inputs":{
                 "LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"a"}}},
                 "RHS":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
             "THEN_BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
               "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
             "ELSIF_CHAIN":{"block":{"type":"kmila_vhdl_elsif","fields":{},
               "inputs":{
                 "COND":{"block":{"type":"kmila_vhdl_eq","fields":{},
                   "inputs":{
                     "LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"b"}}},
                     "RHS":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
                 "BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
                   "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"0"}}}}}}
               }}},
             "ELSE_BODY":{"block":{"type":"kmila_vhdl_if","fields":{},
               "inputs":{
                 "COND":{"block":{"type":"kmila_vhdl_eq","fields":{},
                   "inputs":{
                     "LHS":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"c"}}},
                     "RHS":{"block":{"type":"kmila_vhdl_literal_bit","fields":{"BIT":"1"}}}}}},
                 "THEN_BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
                   "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"Z"}}}}}},
                 "ELSE_BODY":{"block":{"type":"kmila_vhdl_assign_seq","fields":{"TARGET":"y"},
                   "inputs":{"VALUE":{"block":{"type":"kmila_vhdl_id","fields":{"NAME":"Q"}}}}}}
               }}}
           }}
        """);

    // F-8 · T3 — end-to-end Build → Emit on a source with elsif produces
    // VHDL that contains "elsif" and "else" in the right order.
    [Fact]
    public void Emit_F8_IfElsifElse_EmitsElsifThenElse()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; a : in std_logic; b : in std_logic; y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              process (clk) begin
                if a = '1' then
                  y <= '1';
                elsif b = '1' then
                  y <= '0';
                else
                  y <= 'Z';
                end if;
              end process;
            end rtl;
            """;
        var emitted = BuildEmit(src);
        var idxIf    = emitted.IndexOf("if a = '1' then", StringComparison.Ordinal);
        var idxElsif = emitted.IndexOf("elsif b = '1' then", StringComparison.Ordinal);
        var idxElse  = emitted.LastIndexOf("else", StringComparison.Ordinal);
        var idxEndIf = emitted.IndexOf("end if;", StringComparison.Ordinal);
        Assert.True(idxIf    >= 0,        "outer if missing");
        Assert.True(idxElsif >  idxIf,    "elsif must come after if");
        Assert.True(idxElse  >  idxElsif, "else must come after elsif");
        Assert.True(idxEndIf >  idxElse,  "end if; must come after else");
    }

    // F-8 · T4 (fallback fence) — a hand-built LEGACY nested-if workspace
    // still emits correct `if/elsif/else` VHDL through the emitter's
    // fallback branch. This test intentionally does NOT go through the
    // builder (which now produces the new shape) — it validates the
    // legacy code path directly for pre-F-8 .bvhdl sidecars.
    [Fact]
    public void Emit_F8_LegacyNestedIfWorkspace_StillFlattensToElsif()
    {
        var emitted = NewEmitter().Emit(LegacyNestedIfWorkspaceJson());
        Assert.Contains("if a = '1' then", emitted);
        Assert.Contains("elsif b = '1' then", emitted);
        // The "else" arm assigns from identifier `Z` (fixture-only; not a
        // real VHDL literal — the assertion just fingerprints the shape).
        Assert.Contains("else", emitted);
        Assert.Contains("end if;", emitted);
    }

    // F-8 · T5 — full round-trip byte-shape check. Any real elsif-heavy
    // source must round-trip to VHDL that still parses as the same shape
    // (2 elsif arms + one else). Guard: Kmila adapts to the code.
    [Fact]
    public void Emit_F8_TwoElsifRoundTrip_PreservesShape()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; a : in std_logic; b : in std_logic; c : in std_logic; y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              process (clk) begin
                if a = '1' then
                  y <= '1';
                elsif b = '1' then
                  y <= '0';
                elsif c = '1' then
                  y <= '1';
                else
                  y <= '0';
                end if;
              end process;
            end rtl;
            """;
        var emitted = BuildEmit(src);
        // Exactly two "elsif " occurrences.
        int count = System.Text.RegularExpressions.Regex.Matches(emitted, @"\belsif\b").Count;
        Assert.Equal(2, count);
        Assert.Contains("if a = '1' then", emitted);
        Assert.Contains("else", emitted);
        Assert.Contains("end if;", emitted);
    }

    // F-8 · T6 — a mixed workspace (ELSIF_CHAIN + legacy nested-if in
    // ELSE_BODY) emits in the composed order: if / chain-arm / legacy-arm /
    // else. Guards the emitter's "walk chain THEN walk fallback" ordering.
    [Fact]
    public void Emit_F8_MixedWorkspace_EmitsChainThenLegacyArmThenElse()
    {
        var emitted = NewEmitter().Emit(MixedWorkspaceJson());
        var idxIf         = emitted.IndexOf("if a = '1' then",    StringComparison.Ordinal);
        var idxChainArm   = emitted.IndexOf("elsif b = '1' then", StringComparison.Ordinal);
        var idxLegacyArm  = emitted.IndexOf("elsif c = '1' then", StringComparison.Ordinal);
        var idxElse       = emitted.IndexOf("else", idxLegacyArm > 0 ? idxLegacyArm : 0, StringComparison.Ordinal);
        var idxEnd        = emitted.IndexOf("end if;",            StringComparison.Ordinal);
        Assert.True(idxIf        >= 0,           "no outer if in emitted output");
        Assert.True(idxChainArm  >  idxIf,       "chain-arm must come after outer if");
        Assert.True(idxLegacyArm >  idxChainArm, "legacy arm must come after chain arm");
        Assert.True(idxElse      >  idxLegacyArm,"else must come after legacy arm");
        Assert.True(idxEnd       >  idxElse,    "end if; must come after else");
    }

    // Phase 3 roundtrip follow-up (2026-07-23) — a procedure declared
    // inside a process's declarative region must survive Build → Emit →
    // Build with an identical block-fingerprint (V1 == V2). Locks in the
    // emit-side coverage the Builder fix newly enables.
    [Fact]
    public void Emit_ProcedureInsideProcess_RoundTripsByteIdempotent()
    {
        const string src = """
            entity procedure_wait is
                port (q : out std_logic_vector(3 downto 0));
            end procedure_wait;

            architecture rtl of procedure_wait is
                signal state : std_logic := '0';
            begin
                q <= "1010" when state = '1' else "0101";

                process
                    procedure toggle_after (signal s : inout std_logic) is
                    begin
                        wait for 20 ns;
                        if s = '0' then
                            s <= '1';
                        else
                            s <= '0';
                        end if;
                    end procedure;
                begin
                    for i in 0 to 5 loop
                        toggle_after(state);
                    end loop;
                    wait;
                end process;
            end rtl;
            """;

        var v1 = BuildEmit(src);
        var v2 = BuildEmit(v1);
        Assert.Equal(v1, v2);

        // Sanity — the emit path really did render the procedure header
        // + closer (not just accepted an empty program).
        Assert.Contains("procedure toggle_after", v1);
        Assert.Contains("end procedure;", v1);
    }
}
