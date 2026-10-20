using System.Text.Json;
using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Pin the Scratch-VHDL → Blocks direction. F-1..F-7 shipped 2026-06-28 —
// these guard against silent regressions on the emit path used by the
// BlockEditor when the student flips a Code file into Blocks view.
public class VhdlBlockBuilderTests
{
    private static VhdlBlockBuilder NewBuilder() => new(logger: null);

    private static JsonElement RootBlocks(string workspaceJson)
    {
        using var doc = JsonDocument.Parse(workspaceJson);
        return doc.RootElement.GetProperty("blocks").GetProperty("blocks").Clone();
    }

    private static IEnumerable<JsonElement> Walk(JsonElement blocks)
    {
        foreach (var b in blocks.EnumerateArray())
        {
            yield return b;
            if (b.TryGetProperty("inputs", out var inputs))
            {
                foreach (var kv in inputs.EnumerateObject())
                {
                    if (kv.Value.TryGetProperty("block", out var nested))
                        foreach (var inner in Walk(WrapSingle(nested))) yield return inner;
                }
            }
            if (b.TryGetProperty("next", out var next) && next.TryGetProperty("block", out var chained))
                foreach (var inner in Walk(WrapSingle(chained))) yield return inner;
        }
    }

    private static JsonElement WrapSingle(JsonElement single)
    {
        var arr = JsonDocument.Parse("[" + single.GetRawText() + "]").RootElement.Clone();
        return arr;
    }

    [Fact]
    public void Build_NullOrWhitespace_ReturnsEmptyWorkspaceJson()
    {
        var b = NewBuilder();
        Assert.Equal("""{"blocks":{"blocks":[]}}""", b.Build(null));
        Assert.Equal("""{"blocks":{"blocks":[]}}""", b.Build(""));
        Assert.Equal("""{"blocks":{"blocks":[]}}""", b.Build("   \n\t "));
    }

    // F-1: vector type on ports/signals must round-trip through the type field.
    [Fact]
    public void Build_F1_VectorPortType_IsPreserved()
    {
        const string src = """
            entity foo is
              port (a : in std_logic_vector(3 downto 0); y : out std_logic);
            end foo;
            architecture rtl of foo is
              signal buf : std_logic_vector(7 downto 0);
            begin
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        // Any block with a TYPE field carrying "std_logic_vector" survives.
        var carriers = new List<string>();
        foreach (var b in Walk(RootBlocks(json)))
        {
            if (b.TryGetProperty("fields", out var f) &&
                f.TryGetProperty("TYPE", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                carriers.Add(t.GetString() ?? "");
            }
        }
        Assert.Contains(carriers, s => s.Contains("std_logic_vector", StringComparison.OrdinalIgnoreCase));
    }

    // F-2: binary boolean operators become kmila_vhdl_binop with an OP field.
    [Fact]
    public void Build_F2_BinaryOp_ProducesBinopBlockWithOpField()
    {
        const string src = """
            entity foo is
              port (a : in std_logic; b : in std_logic; y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              y <= a and b;
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) &&
            t.GetString() == "kmila_vhdl_binop" &&
            b.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("OP", out var op) &&
            (op.GetString() ?? "").Equals("and", StringComparison.OrdinalIgnoreCase));
        Assert.True(found, "expected a kmila_vhdl_binop with OP=and");
    }

    // 2026-07-19 1:1 audit — `report` is now a first-class block
    // (kmila_vhdl_report). Test now asserts that (a) it produces a
    // report block, NOT a raw fallback, and (b) the sibling assign_seq
    // still survives — the raw-recovery invariant the original F-3 test
    // enforced still holds, just via first-class recognition instead of
    // graceful degradation.
    [Fact]
    public void Build_ReportKeyword_ProducesFirstClassBlock()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              process (clk) begin
                report "hello" severity note;
                y <= '0';
              end process;
            end rtl;
            """;
        var b = NewBuilder();
        var json = b.Build(src);
        Assert.Equal(0, b.LastRawCount);
        var hasReport = Walk(RootBlocks(json)).Any(x =>
            x.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_report");
        Assert.True(hasReport, "expected a first-class kmila_vhdl_report block");
        var assigned = Walk(RootBlocks(json)).Any(x =>
            x.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_assign_seq");
        Assert.True(assigned, "sibling assign_seq must coexist with the report block");
    }

    // F-4: case/when creates a kmila_vhdl_case with at least one when branch.
    [Fact]
    public void Build_F4_CaseWhen_ProducesCaseWithWhenBranch()
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
        var json = NewBuilder().Build(src);
        var hasCase = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_case");
        Assert.True(hasCase, "expected a kmila_vhdl_case block");
    }

    // F-5: vector literals (bit strings) produce a kmila_vhdl_literal_vec.
    [Fact]
    public void Build_F5_VectorLiteral_ProducesLiteralVec()
    {
        const string src = """
            entity foo is
              port (y : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is
            begin
              y <= "1010";
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_literal_vec");
        Assert.True(found, "expected a kmila_vhdl_literal_vec block for \"1010\"");
    }

    // F-7: falling_edge maps to its dedicated block (mirror of rising_edge).
    [Fact]
    public void Build_F7_FallingEdge_ProducesFallingEdgeBlock()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              process (clk) begin
                if falling_edge(clk) then
                  y <= '0';
                end if;
              end process;
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_falling_edge");
        Assert.True(found, "expected a kmila_vhdl_falling_edge block");
    }

    // The Build path must never throw on garbage input — worst case an empty
    // workspace. Guards the "17→1 raw" polish from regressing.
    [Fact]
    public void Build_MalformedSource_DoesNotThrow()
    {
        var b = NewBuilder();
        var json = b.Build("entity ??? is port (");   // truncated / broken
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("blocks", out _));
    }

    // Round-trip guard: rising_edge survives (F-7 parity target).
    [Fact]
    public void Build_RisingEdge_ProducesRisingEdgeBlock()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              process (clk) begin
                if rising_edge(clk) then
                  y <= '1';
                end if;
              end process;
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_rising_edge");
        Assert.True(found, "expected a kmila_vhdl_rising_edge block");
    }

    // F-9 (2026-07-05): arithmetic operators (+, -, *, /, mod, rem) reach
    // the shared kmila_vhdl_binop shape with the correct OP field.
    [Theory]
    [InlineData("a + b", "+")]
    [InlineData("a - b", "-")]
    [InlineData("a * b", "*")]
    [InlineData("a / b", "/")]
    [InlineData("a mod b", "mod")]
    [InlineData("a rem b", "rem")]
    public void Build_F9_ArithOp_ProducesBinopWithOp(string rhs, string expectedOp)
    {
        var src = $"""
            entity foo is
              port (a : in std_logic_vector(3 downto 0); b : in std_logic_vector(3 downto 0);
                    y : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is
            begin
              y <= {rhs};
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_binop" &&
            b.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("OP", out var op) &&
            (op.GetString() ?? "").Equals(expectedOp, StringComparison.OrdinalIgnoreCase));
        Assert.True(found, $"expected binop with OP={expectedOp} for '{rhs}'");
    }

    // F-9 (2026-07-05): non-equality relational operators (/=, <, >, <=, >=).
    [Theory]
    [InlineData("a /= b", "/=")]
    [InlineData("a < b",  "<")]
    [InlineData("a > b",  ">")]
    [InlineData("a <= b", "<=")]
    [InlineData("a >= b", ">=")]
    public void Build_F9_RelationalOp_ProducesBinopWithOp(string rhs, string expectedOp)
    {
        var src = $"""
            entity foo is
              port (a : in std_logic_vector(3 downto 0); b : in std_logic_vector(3 downto 0);
                    y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              y <= '1' when ({rhs}) else '0';
            end rtl;
            """;
        // The `when...else` construct falls to raw_concurrent, but our
        // expression parser reaches the binop via the RHS. Rather than
        // rely on that indirect path, wrap into a process assignment.
        src = $"""
            entity foo is
              port (a : in std_logic_vector(3 downto 0); b : in std_logic_vector(3 downto 0);
                    y : out std_logic);
            end foo;
            architecture rtl of foo is
              signal r : std_logic;
            begin
              process (a, b) begin
                if {rhs} then r <= '1'; end if;
              end process;
              y <= r;
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_binop" &&
            b.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("OP", out var op) &&
            (op.GetString() ?? "").Equals(expectedOp, StringComparison.OrdinalIgnoreCase));
        Assert.True(found, $"expected binop with OP={expectedOp}");
    }

    // F-10 (2026-07-05): vector slice `sig(hi downto lo)` produces a
    // kmila_vhdl_slice block with SIG + DIR fields.
    [Fact]
    public void Build_F10_VectorSlice_ProducesSliceBlock()
    {
        const string src = """
            entity foo is
              port (bus : in std_logic_vector(7 downto 0);
                    y : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is
            begin
              y <= bus(3 downto 0);
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_slice" &&
            b.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("SIG", out var sig) &&
            sig.GetString() == "bus" &&
            f.TryGetProperty("DIR", out var dir) &&
            dir.GetString() == "downto");
        Assert.True(found, "expected a kmila_vhdl_slice with SIG=bus DIR=downto");
    }

    // F-10 (2026-07-05): scalar index `sig(i)` produces a kmila_vhdl_index
    // block with SIG field + IDX sub-expression.
    [Fact]
    public void Build_F10_ScalarIndex_ProducesIndexBlock()
    {
        const string src = """
            entity foo is
              port (bus : in std_logic_vector(7 downto 0); y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              y <= bus(0);
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_index" &&
            b.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("SIG", out var sig) &&
            sig.GetString() == "bus");
        Assert.True(found, "expected a kmila_vhdl_index with SIG=bus");
    }

    // F-12 (2026-07-05): `(others => '0')` aggregate becomes a dedicated
    // kmila_vhdl_others block rather than raw_expr.
    [Theory]
    [InlineData("'0'")]
    [InlineData("'1'")]
    [InlineData("\"0000\"")]
    public void Build_F12_OthersAggregate_ProducesOthersBlock(string val)
    {
        var src = $"""
            entity foo is
              port (rst : in std_logic; clk : in std_logic;
                    q : out std_logic_vector(3 downto 0));
            end foo;
            architecture rtl of foo is
              signal r : std_logic_vector(3 downto 0);
            begin
              process (clk) begin
                if rising_edge(clk) then
                  r <= (others => {val});
                end if;
              end process;
              q <= r;
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var found = Walk(RootBlocks(json)).Any(b =>
            b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_others" &&
            b.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("VAL", out var v) &&
            v.GetString() == val);
        Assert.True(found, $"expected a kmila_vhdl_others with VAL={val}");
    }

    // F-8 (2026-07-05) — helpers to find the outer `kmila_vhdl_if` and pull
    // named inputs off it. Kept private to this file: F-8 is the only test
    // group that cares about the internal shape of the if-block.
    private static JsonElement FindOuterIf(JsonElement blocks)
    {
        foreach (var b in Walk(blocks))
        {
            if (b.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_if")
                return b;
        }
        return default;
    }

    private static bool TryGetInputBlock(JsonElement node, string name, out JsonElement child)
    {
        child = default;
        if (!node.TryGetProperty("inputs", out var inputs)) return false;
        if (!inputs.TryGetProperty(name, out var slot)) return false;
        if (!slot.TryGetProperty("block", out var b)) return false;
        child = b;
        return true;
    }

    // F-8 · T1 — a two-arm if/elsif/else lands as: outer kmila_vhdl_if with
    // one kmila_vhdl_elsif in ELSIF_CHAIN and ELSE_BODY populated. There
    // must be NO nested kmila_vhdl_if inside ELSE_BODY (that was the
    // pre-F-8 "hidden convention" the audit called out).
    [Fact]
    public void Build_F8_IfElsifElse_ProducesLinearChainInElsifSlot()
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
        var json = NewBuilder().Build(src);
        var outerIf = FindOuterIf(RootBlocks(json));
        Assert.True(outerIf.ValueKind == JsonValueKind.Object, "expected an outer kmila_vhdl_if");

        Assert.True(TryGetInputBlock(outerIf, "ELSIF_CHAIN", out var chainHead),
            "ELSIF_CHAIN slot missing on the outer if");
        Assert.Equal("kmila_vhdl_elsif", chainHead.GetProperty("type").GetString());

        Assert.True(TryGetInputBlock(outerIf, "ELSE_BODY", out _),
            "ELSE_BODY slot missing on the outer if");

        // Legacy nested-if inside ELSE_BODY is what we're guarding against.
        Assert.True(TryGetInputBlock(outerIf, "ELSE_BODY", out var elseHead));
        Assert.NotEqual("kmila_vhdl_if", elseHead.GetProperty("type").GetString());
    }

    // F-9 completeness (2026-07-05) — VHDL concatenation `&` is an
    // adding_operator per LRM §9.2. Belongs in the binop family so
    // `reg(6 downto 0) & si` doesn't degrade to raw_expr. Fixture
    // matches the 8-bit shift register EXAMPLE_SHIFT_VHD in
    // ProjectsView.razor which was reported as producing 1 raw fragment.
    [Fact]
    public void Build_ConcatOperator_ProducesBinopBlock()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; si : in std_logic;
                    q : out std_logic_vector(7 downto 0));
            end foo;
            architecture rtl of foo is
              signal reg : std_logic_vector(7 downto 0);
            begin
              process (clk) begin
                if rising_edge(clk) then
                  reg <= reg(6 downto 0) & si;
                end if;
              end process;
              q <= reg;
            end rtl;
            """;
        var b = NewBuilder();
        var json = b.Build(src);
        // The `&` must become a binop with OP="&".
        var found = Walk(RootBlocks(json)).Any(n =>
            n.TryGetProperty("type", out var t) && t.GetString() == "kmila_vhdl_binop" &&
            n.TryGetProperty("fields", out var f) &&
            f.TryGetProperty("OP", out var op) && op.GetString() == "&");
        Assert.True(found, "expected a kmila_vhdl_binop with OP=&");
    }

    // Regression: exact EXAMPLE_SHIFT_VHD from ProjectsView.razor must
    // round-trip with zero raw fragments after the F-9/F-10 completeness
    // work (2026-07-05: `&` added to AddingOps, integer literals
    // recognized). User reported "1 fragment couldn't be turned into a
    // block" — actually 2 raws pre-fix: HI/LO integer literals of the
    // slice `reg(6 downto 0)`. Both closed.
    //
    // 2026-07-06 batch 2: baseline lifted 0 → 1 by the scan/original
    // alignment fix in TryParseIf. The `-- Shift left, SI enters at the
    // LSB.` comment sits before the FIRST statement of the innermost
    // if-body; pre-fix, the parallel `.Trim()` calls silently ate it.
    // Post-fix, that comment survives as a raw_seq block — the count
    // going UP here is a data-preservation win, not a regression.
    [Fact]
    public void Build_ShiftRegisterExample_ProducesOnlyCommentRaw()
    {
        const string src = """
            entity shift_register is
              port (
                clk      : in  STD_LOGIC;
                reset    : in  STD_LOGIC;
                shift_en : in  STD_LOGIC;
                si       : in  STD_LOGIC;
                reg_out  : out STD_LOGIC_VECTOR(7 downto 0)
              );
            end entity shift_register;

            architecture behavioral of shift_register is
              signal reg : STD_LOGIC_VECTOR(7 downto 0) := (others => '0');
            begin
              process(clk, reset)
              begin
                if reset = '1' then
                  reg <= (others => '0');
                elsif rising_edge(clk) then
                  if shift_en = '1' then
                    -- Shift left, SI enters at the LSB.
                    reg <= reg(6 downto 0) & si;
                  end if;
                end if;
              end process;

              reg_out <= reg;
            end architecture behavioral;
            """;
        var b = NewBuilder();
        var json = b.Build(src);
        // 2026-07-19 Phase 9 — the `-- Shift left…` comment is now a
        // first-class kmila_vhdl_comment_seq block, not a raw. Assert
        // the comment block exists and no raws remain.
        Assert.Equal(0, b.LastRawCount);
        var hasComment = Walk(RootBlocks(json)).Any(x =>
            x.TryGetProperty("type", out var t)
            && t.GetString() == "kmila_vhdl_comment_seq");
        Assert.True(hasComment, "expected a first-class kmila_vhdl_comment_seq block");
    }

    // F-8 · T2 — three elsif arms produce a linear next-chain of length 3.
    [Fact]
    public void Build_F8_ThreeElsifArms_ProduceLinearChainOfLengthThree()
    {
        const string src = """
            entity foo is
              port (clk : in std_logic; sel : in std_logic_vector(1 downto 0); y : out std_logic);
            end foo;
            architecture rtl of foo is
            begin
              process (clk) begin
                if sel = "00" then
                  y <= '0';
                elsif sel = "01" then
                  y <= '1';
                elsif sel = "10" then
                  y <= '0';
                elsif sel = "11" then
                  y <= '1';
                end if;
              end process;
            end rtl;
            """;
        var json = NewBuilder().Build(src);
        var outerIf = FindOuterIf(RootBlocks(json));
        Assert.True(TryGetInputBlock(outerIf, "ELSIF_CHAIN", out var chainHead));

        int count = 0;
        var cur = chainHead;
        while (cur.ValueKind == JsonValueKind.Object)
        {
            Assert.Equal("kmila_vhdl_elsif", cur.GetProperty("type").GetString());
            count++;
            if (cur.TryGetProperty("next", out var next) && next.TryGetProperty("block", out var nxt))
                cur = nxt;
            else
                break;
        }
        Assert.Equal(3, count);
    }

    // ── 2026-07-19 Phase 22 — direct microtests for Phase 19 blocks ──

    [Fact]
    public void Build_P19_ComponentDecl_IsRecognizedInArchDecls()
    {
        const string src = """
            entity tb is end entity;
            architecture sim of tb is
                component uut is
                    port (a : in std_logic; y : out std_logic);
                end component;
            begin
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var hasCompDecl = Walk(RootBlocks(json)).Any(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_component_decl"
            && b.GetProperty("fields").GetProperty("NAME").GetString() == "uut");
        Assert.True(hasCompDecl, "component_decl block not produced in arch DECLS");
    }

    [Fact]
    public void Build_P19_ProcedureDecl_WithParamsAndBody()
    {
        const string src = """
            entity tb is end entity;
            architecture sim of tb is
                procedure p(a : in std_logic; b : in std_logic) is
                begin
                    wait for 1 ns;
                end procedure;
            begin
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var proc = Walk(RootBlocks(json)).FirstOrDefault(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_procedure_decl");
        Assert.NotEqual(JsonValueKind.Undefined, proc.ValueKind);
        Assert.Equal("p", proc.GetProperty("fields").GetProperty("NAME").GetString());
    }

    [Fact]
    public void Build_P19_FunctionDecl_CarriesReturnType()
    {
        const string src = """
            entity dummy is end entity;
            architecture rtl of dummy is
                function f(x : in std_logic) return integer is
                begin
                    return 0;
                end function;
            begin
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var fn = Walk(RootBlocks(json)).FirstOrDefault(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_function_decl");
        Assert.NotEqual(JsonValueKind.Undefined, fn.ValueKind);
        Assert.Equal("integer",
            fn.GetProperty("fields").GetProperty("RETURN_TYPE").GetString());
    }

    [Fact]
    public void Build_P19_ProcedureCall_InSequentialBody()
    {
        const string src = """
            entity tb is end entity;
            architecture sim of tb is
                procedure ping(x : in std_logic) is
                begin
                    wait for 1 ns;
                end procedure;
            begin
                stim: process
                begin
                    ping('1');
                    wait;
                end process;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var call = Walk(RootBlocks(json)).FirstOrDefault(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_procedure_call");
        Assert.NotEqual(JsonValueKind.Undefined, call.ValueKind);
        Assert.Equal("ping",
            call.GetProperty("fields").GetProperty("NAME").GetString());
    }

    // ── Phase 3 roundtrip follow-up (2026-07-23) ──
    // procedure declared INSIDE a process declarative region. Before the
    // fix, ProcessRe's non-greedy `.*?\bbegin\b` split at the procedure
    // body's `begin`, leaving the procedure header as raw_decl and the
    // closer `end procedure;` as two raw_seq tiles. Cases 144/145 in
    // SimulatorBench are the load-bearing --roundtrip fences at engine
    // level; these tests pin the Builder-side contract.

    [Fact]
    public void Build_ProcedureInsideProcess_LandsInProcessDeclsSlot()
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

        var b = NewBuilder();
        var json = b.Build(src);

        // Load-bearing assertion — no raws.
        Assert.Equal(0, b.LastRawCount);

        // The process's DECLS slot carries a procedure_decl named
        // toggle_after, and its BODY slot starts with the for-loop
        // (not with the procedure body spliced inline).
        var proc = Walk(RootBlocks(json)).FirstOrDefault(x =>
            x.GetProperty("type").GetString() == "kmila_vhdl_process");
        Assert.NotEqual(JsonValueKind.Undefined, proc.ValueKind);

        var procInputs = proc.GetProperty("inputs");
        var declsHead = procInputs.GetProperty("DECLS").GetProperty("block");
        Assert.Equal("kmila_vhdl_procedure_decl", declsHead.GetProperty("type").GetString());
        Assert.Equal("toggle_after",
            declsHead.GetProperty("fields").GetProperty("NAME").GetString());

        var bodyHead = procInputs.GetProperty("BODY").GetProperty("block");
        Assert.Equal("kmila_vhdl_for_loop", bodyHead.GetProperty("type").GetString());
    }

    [Fact]
    public void Build_ProcedureInsideProcess_MultipleSubprograms()
    {
        // Stack-tracker regression fence — process declares TWO
        // subprograms (a procedure and a function) before the process's
        // own `begin`. The depth-0 begin-finder must skip past both.
        const string src = """
            entity tb is end entity;
            architecture sim of tb is
                signal s : std_logic := '0';
            begin
                process
                    procedure p_a (signal x : inout std_logic) is
                    begin
                        wait for 5 ns;
                        x <= '1';
                    end procedure;
                    procedure p_b (signal x : inout std_logic) is
                    begin
                        wait for 3 ns;
                        x <= '0';
                    end procedure;
                begin
                    p_a(s);
                    p_b(s);
                    wait;
                end process;
            end architecture;
            """;

        var b = NewBuilder();
        var json = b.Build(src);

        Assert.Equal(0, b.LastRawCount);

        var proc = Walk(RootBlocks(json)).FirstOrDefault(x =>
            x.GetProperty("type").GetString() == "kmila_vhdl_process");
        Assert.NotEqual(JsonValueKind.Undefined, proc.ValueKind);

        // Both procedure_decls must appear in the DECLS chain.
        var declsNames = Walk(RootBlocks(json))
            .Where(x => x.GetProperty("type").GetString() == "kmila_vhdl_procedure_decl")
            .Select(x => x.GetProperty("fields").GetProperty("NAME").GetString())
            .ToList();
        Assert.Contains("p_a", declsNames);
        Assert.Contains("p_b", declsNames);
    }

    // ── 2026-07-19 Phase 22 — post-P19 corpus-reduction guardrails ──

    // 20b: process declarative part (variables between `process` and `begin`).
    [Fact]
    public void Build_P20b_ProcessDeclarativePart_ParsesVariableDecls()
    {
        const string src = """
            entity tb is end entity;
            architecture sim of tb is
            begin
                stim: process
                    variable v : integer := 0;
                begin
                    wait;
                end process;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var hasVar = Walk(RootBlocks(json)).Any(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_variable_decl"
            && b.GetProperty("fields").GetProperty("NAME").GetString() == "v");
        Assert.True(hasVar, "variable_decl inside process not produced");
    }

    // 20c: nested for-loops require depth-tracking in FindEndKeyword.
    [Fact]
    public void Build_P20c_NestedForLoops_MatchOuterEndLoop()
    {
        const string src = """
            entity tb is end entity;
            architecture sim of tb is
            begin
                stim: process
                begin
                    for i in 0 to 3 loop
                        for j in 0 to 3 loop
                            wait for 1 ns;
                        end loop;
                    end loop;
                    wait;
                end process;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        // Two for_loop blocks should be produced (outer + inner). If
        // FindEndKeyword returned inner's `end loop` for the outer,
        // only ONE would exist and the rest would be raw_seq.
        var forLoops = Walk(RootBlocks(json))
            .Count(b => b.GetProperty("type").GetString() == "kmila_vhdl_for_loop");
        Assert.Equal(2, forLoops);
    }

    // 20c: multi-arg calls (`to_unsigned(x, 8)`) must not be misread as index.
    [Fact]
    public void Build_P20c_MultiArgCall_IsRecognizedAsCallNotIndex()
    {
        const string src = """
            entity foo is port (y : out std_logic_vector(7 downto 0)); end entity;
            architecture rtl of foo is
            begin
                y <= std_logic_vector(to_unsigned(5, 8));
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var hasMultiArgCall = Walk(RootBlocks(json)).Any(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_call"
            && b.GetProperty("fields").GetProperty("NAME").GetString() == "to_unsigned");
        Assert.True(hasMultiArgCall, "to_unsigned(5, 8) not recognized as multi-arg call");
    }

    // 20c: attribute-select `<name>'<attr>` accepted as a bare identifier.
    [Fact]
    public void Build_P20c_AttributeSelect_ParsesAsId()
    {
        const string src = """
            entity foo is port (a : in std_logic_vector(3 downto 0); n : out integer); end entity;
            architecture rtl of foo is
            begin
                n <= a'length;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var hasAttrId = Walk(RootBlocks(json)).Any(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_id"
            && b.GetProperty("fields").GetProperty("NAME").GetString() == "a'length");
        Assert.True(hasAttrId, "attribute-select `a'length` not parsed as identifier");
    }

    // 20c: `**` exponentiation must not be mis-split as `*`.
    [Fact]
    public void Build_P20c_ExponentOperator_ParsesAsBinop()
    {
        const string src = """
            entity foo is port (n : out integer); end entity;
            architecture rtl of foo is
            begin
                n <= 2 ** 8;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var hasPowBinop = Walk(RootBlocks(json)).Any(b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_binop"
            && b.GetProperty("fields").GetProperty("OP").GetString() == "**");
        Assert.True(hasPowBinop, "`2 ** 8` not parsed as binop with OP='**'");
    }

    // Pass 18 (2026-07-30) — nested-paren LHS on concurrent assign. The
    // pre-pass-18 regex used `\([^()]*\)` for the LHS index and silently
    // dropped `mem(to_integer(unsigned(addr))) <= din;` into
    // kmila_vhdl_raw_concurrent instead of kmila_vhdl_assign_concurrent.
    [Fact]
    public void Build_P18_ConcurrentAssign_NestedParenLhs_KeepsBlockStructured()
    {
        const string src = """
            library ieee;
            use ieee.std_logic_1164.all;
            use ieee.numeric_std.all;
            entity foo is
              port (addr : in std_logic_vector(2 downto 0);
                    din  : in std_logic_vector(7 downto 0));
            end entity;
            architecture rtl of foo is
              type mem_t is array (0 to 7) of std_logic_vector(7 downto 0);
              signal mem : mem_t := (others => (others => '0'));
            begin
              mem(to_integer(unsigned(addr))) <= din;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var blocks = Walk(RootBlocks(json)).ToList();
        Assert.DoesNotContain(blocks, b => b.GetProperty("type").GetString() == "kmila_vhdl_raw_concurrent");
        Assert.Contains(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_assign_concurrent"
            && b.GetProperty("fields").GetProperty("TARGET").GetString()
                == "mem(to_integer(unsigned(addr)))");
    }

    // Pass 18 (2026-07-30) — nested-paren LHS on sequential `<=` inside
    // process. Fences AssignSeqRe (VhdlBlockBuilder.cs:1923).
    [Fact]
    public void Build_P18_SequentialAssign_NestedParenLhs_KeepsBlockStructured()
    {
        const string src = """
            library ieee;
            use ieee.std_logic_1164.all;
            use ieee.numeric_std.all;
            entity foo is
              port (clk : in std_logic;
                    addr : in std_logic_vector(2 downto 0);
                    din  : in std_logic_vector(7 downto 0));
            end entity;
            architecture rtl of foo is
              type mem_t is array (0 to 7) of std_logic_vector(7 downto 0);
              signal mem : mem_t := (others => (others => '0'));
            begin
              process(clk) begin
                if rising_edge(clk) then
                  mem(to_integer(unsigned(addr))) <= din;
                end if;
              end process;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var blocks = Walk(RootBlocks(json)).ToList();
        // Any raw_seq means the LHS regex silently dropped the statement.
        Assert.DoesNotContain(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_raw_seq"
            && (b.GetProperty("fields").GetProperty("TEXT").GetString() ?? "")
                .Contains("mem(to_integer"));
        Assert.Contains(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_assign_seq"
            && b.GetProperty("fields").GetProperty("TARGET").GetString()
                == "mem(to_integer(unsigned(addr)))");
    }

    // Pass 21 (2026-07-30) — nested-paren slice bound `sig(to_integer(x) downto 0)`.
    [Fact]
    public void Build_P21_Slice_NestedParenHi_ParsesAsSlice()
    {
        const string src = """
            library ieee;
            use ieee.std_logic_1164.all;
            use ieee.numeric_std.all;
            entity foo is
              port (n : in std_logic_vector(3 downto 0);
                    y : out std_logic_vector(3 downto 0));
            end entity;
            architecture rtl of foo is
              signal s : std_logic_vector(7 downto 0);
            begin
              y <= s(to_integer(unsigned(n)) + 3 downto to_integer(unsigned(n)));
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var blocks = Walk(RootBlocks(json)).ToList();
        Assert.Contains(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_slice"
            && b.GetProperty("fields").GetProperty("SIG").GetString() == "s"
            && b.GetProperty("fields").GetProperty("DIR").GetString() == "downto");
    }

    // Pass 21 (2026-07-30) — nested-paren edge detector `rising_edge(clk_bus(0))`.
    [Fact]
    public void Build_P21_RisingEdge_NestedParenArg_ParsesAsEdge()
    {
        const string src = """
            library ieee;
            use ieee.std_logic_1164.all;
            entity foo is
              port (clk_bus : in std_logic_vector(1 downto 0);
                    q       : out std_logic);
            end entity;
            architecture rtl of foo is
              signal r : std_logic;
            begin
              process(clk_bus)
              begin
                if rising_edge(clk_bus(0)) then
                  r <= not r;
                end if;
              end process;
              q <= r;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var blocks = Walk(RootBlocks(json)).ToList();
        Assert.Contains(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_rising_edge");
    }

    // Pass 18 (2026-07-30) — nested-paren LHS on variable `:=` inside
    // process. Fences VariableAssignSeqRe (VhdlBlockBuilder.cs:1929).
    // Also fences pass-19's process-local RAM_WRITE feature at the parse
    // level; synth-side lowering lands in pass 19.
    [Fact]
    public void Build_P18_VariableAssign_NestedParenLhs_KeepsBlockStructured()
    {
        const string src = """
            library ieee;
            use ieee.std_logic_1164.all;
            use ieee.numeric_std.all;
            entity foo is
              port (clk : in std_logic;
                    addr : in std_logic_vector(2 downto 0);
                    din  : in std_logic_vector(7 downto 0));
            end entity;
            architecture rtl of foo is
            begin
              process(clk)
                type mem_t is array (0 to 7) of std_logic_vector(7 downto 0);
                variable mem : mem_t := (others => (others => '0'));
              begin
                if rising_edge(clk) then
                  mem(to_integer(unsigned(addr))) := din;
                end if;
              end process;
            end architecture;
            """;
        var json = NewBuilder().Build(src);
        var blocks = Walk(RootBlocks(json)).ToList();
        Assert.DoesNotContain(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_raw_seq"
            && (b.GetProperty("fields").GetProperty("TEXT").GetString() ?? "")
                .Contains("mem(to_integer"));
        Assert.Contains(blocks, b =>
            b.GetProperty("type").GetString() == "kmila_vhdl_variable_assign"
            && b.GetProperty("fields").GetProperty("TARGET").GetString()
                == "mem(to_integer(unsigned(addr)))");
    }
}
