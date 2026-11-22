using Interpreter.Services;
using Parser.Models;

namespace Interpreter.Tests;

/// <summary>
/// Regression coverage for the P1 syntax-strictness hardening (2026-10-20).
/// The build path (Interpreter synthesis: <see cref="Architecture"/> →
/// <see cref="Process"/> / IfStructure / CaseStructure) used to "consume
/// expected tokens only when present, never error on absence", so several
/// pure-syntax errors GHDL rejects were silently accepted. These tests drive
/// <see cref="Entity.Syntetize(string[])"/> + <see cref="Architecture.Syntetize(string[])"/>
/// (the same calls the Debugger line-scanner makes) and assert each bad case
/// now throws, while well-formed code still parses.
///
/// Fixtures mirror Kmila-9s/ReviewSyntaxGap/ (S1, S4, S6, S9) plus a valid
/// control. The one-line-entity scanner fix lives in the Debugger module and is
/// regression-covered by the CLI fixture gate, not here (Interpreter cannot
/// reference Debugger).
/// </summary>
public static class SyntaxStrictnessTests
{
    /// <summary>Runs every syntax-strictness test, printing pass/fail for each.</summary>
    /// <returns>The number of tests that failed.</returns>
    public static int RunAllTests()
    {
        int failed = 0;
        Console.WriteLine("=== Syntax Strictness Tests ===");

        var tests = new (string Name, Func<bool> Run)[]
        {
            ("S1: missing ';' after 'end process' is rejected", Test_MissingSemicolonEndProcess),
            ("S4: missing '=>' in a case choice is rejected",   Test_MissingArrowInCase),
            ("S6: ':=' on a signal (concurrent) is rejected",   Test_VarAssignOnSignal),
            ("S9: missing 'end if;' is rejected",               Test_MissingEndIf),
            ("C1: undeclared case choice is rejected",          Test_UndeclaredCaseChoice),
            ("C7: duplicate case choice is rejected",           Test_DuplicateCaseChoice),
            ("C6: non-exhaustive std_logic_vector case rejected", Test_NonExhaustiveCase),
            ("C10: enum choice not in the type is rejected",    Test_EnumChoiceNotInType),
            ("precise-C10: cross-enum literal rejected",        Test_C10_CrossEnum),
            ("precise-C10: cross-enum in a '|' alt rejected",   Test_C10_CrossEnumAlt),
            ("precise-C10: same-enum '|' alts still parse",     Test_C10_SameEnumParses),
            ("C2: undeclared RHS signal is rejected",           Test_UndeclaredRhsSignal),
            ("C3: undeclared assignment target is rejected",    Test_UndeclaredLhsTarget),
            ("C9: undefined function call is rejected",         Test_UndefinedFunctionCall),
            ("C8: assignment to an input port is rejected",     Test_AssignToInputPort),
            ("C4: scalar <= vector type mismatch is rejected",  Test_TypeMismatchScalarVector),
            ("C5: literal width mismatch is rejected",          Test_WidthMismatchLiteral),
            ("C11: missing ';' on a signal assignment rejected",Test_MissingSemicolonConcurrent),
            ("C11 via when…else: missing ';' rejected",         Test_MissingSemicolon_WhenElse),
            ("C11 via with…select: missing ';' rejected",       Test_MissingSemicolon_WithSelect),
            ("C11 via with…select: content after others rejected", Test_MissingSemicolon_WithSelectOthers),
            ("C11 trailing-condition (no else), str operand rejected", Test_MissingSemicolon_TrailingCond_Str),
            ("C11 trailing-condition (no else), id operand rejected", Test_MissingSemicolon_TrailingCond_Id),
            ("C11 trailing-comparison then next stmt rejected",  Test_MissingSemicolon_TrailingCmp),
            ("C11 in a for-generate body rejected",              Test_Gen_MissingSemicolon),
            ("C8 in a generate body (assign input port) rejected", Test_Gen_AssignInputPort),
            ("C11 on a with…select in a generate rejected",      Test_Gen_WithSelectMissingSemicolon),
            ("C5 concatenation width mismatch rejected",         Test_Width_ConcatMismatch),
            ("C4 concatenation into a scalar rejected",          Test_Width_ConcatIntoScalar),
            ("C5 literal-slice width mismatch rejected",         Test_Width_SliceMismatch),
            ("C5 hex-literal width mismatch rejected",           Test_Width_HexMismatch),
            ("C11 indexed next-target (missing ';') rejected",   Test_C11_IndexedNextTarget),
            ("C8 via when…else: assign to input port rejected", Test_AssignToInputPort_WhenElse),
            ("C8 via with…select: assign to input port rejected", Test_AssignToInputPort_WithSelect),
            ("C4 via when…else: scalar <= vector rejected",     Test_TypeMismatch_WhenElse),
            ("C4 via with…select: scalar <= vector rejected",   Test_TypeMismatch_WithSelect),
            ("C5 via when…else: literal width mismatch rejected", Test_WidthMismatch_WhenElse),
            ("C5 via with…select: literal width mismatch rejected", Test_WidthMismatch_WithSelect),
            ("Valid if/case/process still parses",              Test_ValidControlParses),
            ("Valid literal case (with others) still parses",   Test_ValidLiteralCaseParses),
            ("Declared constant + signal still resolve",        Test_DeclaredConstantResolves),
            ("Reading an input port on the RHS still parses",   Test_ReadInputPortParses),
            ("Matching-width vector assignment still parses",   Test_MatchingWidthParses),
            ("Expression RHS is not width-checked",             Test_ExpressionRhsNotWidthChecked),
            ("Conditional '<=' comparison still parses",        Test_ConditionalComparisonParses),
            ("Valid when…else assignment still parses",         Test_ValidWhenElseParses),
            ("Valid with…select assignment still parses",       Test_ValidWithSelectParses),
            ("Metavalue char literal 'X' is scalar, not vector", Test_MetavalueCharLiteralParses),
            ("with…select with 'X' when others still parses",   Test_WithSelectMetavalueOthersParses),
            ("when…else with 'after' delay still parses",        Test_WhenElseAfterDelayParses),
            ("with…select with 'after' delay still parses",      Test_WithSelectAfterDelayParses),
            ("condition '<=' comparison (guarded) still parses", Test_GuardedConditionComparisonParses),
            ("condition 'and' before '<=' still parses",         Test_GuardedAndBeforeComparisonParses),
            ("condition 'mod' before '<=' still parses",         Test_GuardedModBeforeComparisonParses),
            ("condition indexed operand before '<=' still parses", Test_GuardedIndexOperandParses),
            ("valid nested generate (decls + begin) still parses", Test_Gen_NestedValidParses),
            ("valid with…select in a generate still parses",      Test_Gen_WithSelectValidParses),
            ("matching-width concatenation still parses",        Test_Width_ConcatMatchParses),
            ("concat with a non-literal slice still parses",     Test_Width_NonLiteralSliceParses),
            ("arithmetic / conversion RHS not width-checked",    Test_Width_ArithConversionParses),
            ("matching hex-literal width still parses",          Test_Width_HexMatchParses),
            ("indexed comparison in a condition still parses",   Test_C11_IndexComparisonParses),
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

    // Shared entity used by the architecture-level cases.
    private static readonly string[] Ent2 =
    {
        "entity dut is",
        "    port (a : in std_logic; sel : in std_logic_vector(1 downto 0); y : out std_logic);",
        "end entity dut;",
    };

    // --- negative cases: synthesis must throw ---

    private static bool Test_MissingSemicolonEndProcess() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(a)",
        "    begin",
        "        y <= a;",
        "    end process",          // ERROR: missing ';'
        "end architecture rtl;",
    });

    private static bool Test_MissingArrowInCase() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(sel)",
        "    begin",
        "        case sel is",
        "            when \"00\"    y <= '0';",   // ERROR: missing '=>'
        "            when others => y <= '1';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    private static bool Test_VarAssignOnSignal() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y := a;",              // ERROR: ':=' on a signal in a concurrent statement
        "end architecture rtl;",
    });

    private static bool Test_MissingEndIf() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(a)",
        "    begin",
        "        if a = '1' then",
        "            y <= '1';",
        "    end process;",          // ERROR: 'end if;' missing before 'end process'
        "end architecture rtl;",
    });

    private static bool Test_UndeclaredCaseChoice() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(sel)",
        "    begin",
        "        case sel is",
        "            when \"00\" => y <= '0';",
        "            when C     => y <= '1';",   // ERROR: C is undeclared
        "            when others => y <= '0';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    private static bool Test_DuplicateCaseChoice() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(sel)",
        "    begin",
        "        case sel is",
        "            when \"00\" => y <= '0';",
        "            when \"00\" => y <= '1';",   // ERROR: choice "00" already covered
        "            when others => y <= '0';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    private static bool Test_NonExhaustiveCase() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(sel)",
        "    begin",
        "        case sel is",
        "            when \"00\" => y <= '0';",
        "            when \"01\" => y <= '1';",   // ERROR: "10"/"11" uncovered, no others
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    private static bool Test_EnumChoiceNotInType() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    type state_t is (IDLE, RUN, DONE);",
        "    signal st : state_t := IDLE;",
        "begin",
        "    process(st)",
        "    begin",
        "        case st is",
        "            when IDLE   => y <= '0';",
        "            when STOP   => y <= '1';",   // ERROR: STOP is not a literal of state_t
        "            when others => y <= '0';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    // --- positive controls: synthesis must NOT throw ---

    private static bool Test_ValidLiteralCaseParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(sel)",
        "    begin",
        "        case sel is",
        "            when \"00\" => y <= '0';",
        "            when \"01\" => y <= '1';",
        "            when others => y <= '0';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    private static bool Test_ValidControlParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(a, sel)",
        "    begin",
        "        case sel is",
        "            when \"00\" => y <= '0';",
        "            when others => y <= a;",
        "        end case;",
        "        if sel(0) = '1' then",
        "            y <= '1';",
        "        end if;",
        "    end process;",
        "end architecture rtl;",
    });

    // P3 (v1.17.0): undeclared-identifier rejection.

    private static bool Test_UndeclaredRhsSignal() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= a and enable_x;",   // ERROR: enable_x never declared
        "end architecture rtl;",
    });

    private static bool Test_UndeclaredLhsTarget() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    process(a)",
        "    begin",
        "        counter_next <= a;",  // ERROR: counter_next never declared
        "    end process;",
        "end architecture rtl;",
    });

    private static bool Test_UndefinedFunctionCall() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= my_decode(a);",      // ERROR: my_decode defined nowhere
        "end architecture rtl;",
    });

    private static bool Test_AssignToInputPort() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    a <= '0';",                // ERROR: `a` is mode `in`
        "end architecture rtl;",
    });

    private static bool Test_TypeMismatchScalarVector() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= sel;",                 // ERROR: std_logic <= std_logic_vector
        "end architecture rtl;",
    });

    private static bool Test_WidthMismatchLiteral() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    signal v3 : std_logic_vector(2 downto 0);",
        "begin",
        "    v3 <= \"1010\";",            // ERROR: 4-bit literal into a 3-bit signal
        "end architecture rtl;",
    });

    // Positive guard: a matching-width literal assignment must still parse.
    private static bool Test_MatchingWidthParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    signal v3 : std_logic_vector(2 downto 0);",
        "begin",
        "    v3 <= \"101\";",
        "end architecture rtl;",
    });

    // Positive guard: an expression RHS (concatenation) must be skipped by the
    // single-atom type/width check, never width-rejected.
    private static bool Test_ExpressionRhsNotWidthChecked() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    signal v3 : std_logic_vector(2 downto 0);",
        "begin",
        "    v3 <= sel & a;",
        "end architecture rtl;",
    });

    private static bool Test_MissingSemicolonConcurrent() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= a and sel(0)",         // ERROR: missing ';'
        "    y <= a;",
        "end architecture rtl;",
    });

    // C11 via when…else: a missing ';' on an else-terminated conditional runs the
    // RHS past into the next statement; the depth-0 '<=' in the waveform context
    // (not inside a condition) is detected.
    private static bool Test_MissingSemicolon_WhenElse() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= '1' when sel = \"00\" else '0'",   // ERROR: missing ';'
        "    y <= a;",
        "end architecture rtl;",
    });

    // C11 via with…select: a missing ';' leaks the next statement's '<=' into the
    // choice list, which is never valid there.
    private static bool Test_MissingSemicolon_WithSelect() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select y <= '1' when \"00\", '0' when \"01\"",   // ERROR: missing ';'
        "    y <= a;",
        "end architecture rtl;",
    });

    // C11 via with…select: nothing may follow an `others` choice except ';'.
    private static bool Test_MissingSemicolon_WithSelectOthers() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select y <= '1' when \"00\", '0' when others",   // ERROR: missing ';'
        "    y <= a;",
        "end architecture rtl;",
    });

    // Positive guard: a conditional assignment whose condition uses '<=' as a
    // comparison must still parse — the missing-';' check must not flag it.
    private static bool Test_ConditionalComparisonParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    signal v3 : std_logic_vector(2 downto 0);",
        "begin",
        "    y <= '1' when v3 <= \"010\" else '0';",
        "end architecture rtl;",
    });

    // Positive guard: reading an input port on the RHS (and driving an output)
    // must still parse — the port-mode check must only reject in-port *targets*.
    private static bool Test_ReadInputPortParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= a and sel(0);",
        "end architecture rtl;",
    });

    // Positive guard: a declared local constant and signal must still resolve
    // (the undeclared check must not over-reject real declarations).
    private static bool Test_DeclaredConstantResolves() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    constant K : std_logic := '1';",
        "    signal tmp : std_logic;",
        "begin",
        "    tmp <= a and K;",
        "    y <= tmp;",
        "end architecture rtl;",
    });

    // --- item 3: C8/C4/C5 now also reach when…else and with…select forms ---

    // C8 via when…else: driving an input-port *target* is illegal even inside a
    // conditional assignment.
    private static bool Test_AssignToInputPort_WhenElse() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    a <= '0' when sel = \"00\" else '1';",   // ERROR: `a` is mode `in`
        "end architecture rtl;",
    });

    // C8 via with…select.
    private static bool Test_AssignToInputPort_WithSelect() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select a <= '0' when \"00\", '1' when others;",  // ERROR: `a` is mode `in`
        "end architecture rtl;",
    });

    // C4 via when…else: scalar target ⇐ vector value.
    private static bool Test_TypeMismatch_WhenElse() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= sel when a = '1' else sel;",        // ERROR: std_logic <= std_logic_vector
        "end architecture rtl;",
    });

    // C4 via with…select.
    private static bool Test_TypeMismatch_WithSelect() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select y <= sel when \"00\", sel when others;",  // ERROR: std_logic <= std_logic_vector
        "end architecture rtl;",
    });

    // C5 via when…else: 4-bit literal into a 3-bit target.
    private static bool Test_WidthMismatch_WhenElse() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    signal v3 : std_logic_vector(2 downto 0);",
        "begin",
        "    v3 <= \"1010\" when a = '1' else \"000\";",  // ERROR: 4-bit literal into 3-bit signal
        "end architecture rtl;",
    });

    // C5 via with…select.
    private static bool Test_WidthMismatch_WithSelect() => ExpectThrows(Ent2, new[]
    {
        "architecture rtl of dut is",
        "    signal v3 : std_logic_vector(2 downto 0);",
        "begin",
        "    with sel select v3 <= \"1010\" when \"00\", \"000\" when others;",  // ERROR: 4-bit literal
        "end architecture rtl;",
    });

    // Positive guard: a valid when…else (matching types/widths, output target)
    // must still parse — the new checks must not over-reject.
    private static bool Test_ValidWhenElseParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= '1' when sel = \"00\" else '0';",
        "end architecture rtl;",
    });

    // Positive guard: a valid with…select must still parse.
    private static bool Test_ValidWithSelectParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select y <= '1' when \"00\", '0' when others;",
        "end architecture rtl;",
    });

    // Positive guard (root-cause fix): a single-quoted std_logic metavalue
    // literal ('X','W','L','H', …) is a SCALAR, not a vector, so assigning it to
    // a scalar target must not trip the C4 type check. The datatype finder
    // mislabels these; the assignment check corrects it from the raw source.
    private static bool Test_MetavalueCharLiteralParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= 'X';",
        "end architecture rtl;",
    });

    // Positive guard: a with…select whose default is a metavalue char literal
    // (the `'X' when others` shape used by real mux designs, e.g. bench 014).
    private static bool Test_WithSelectMetavalueOthersParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select y <= '1' when \"00\", 'X' when others;",
        "end architecture rtl;",
    });

    // Positive guard: a when…else waveform with an `after` delay must still parse —
    // the C11 state machine must not mistake `after`/`ns` for a statement boundary.
    private static bool Test_WhenElseAfterDelayParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    y <= '1' after 5 ns when sel = \"00\" else '0';",
        "end architecture rtl;",
    });

    // Positive guard: a with…select waveform with an `after` delay must still parse
    // (the shape used by real mux designs, e.g. bench 014-mux-sel).
    private static bool Test_WithSelectAfterDelayParses() => ExpectParses(Ent2, new[]
    {
        "architecture rtl of dut is",
        "begin",
        "    with sel select y <= '1' after 5 ns when \"00\", '0' when others;",
        "end architecture rtl;",
    });

    // --- item A: guarded trailing-condition heuristic (missing ';' with a '<=' next stmt) ---

    // Entity with a second output, a boolean, and integer operands so conditions
    // can legitimately use '<=' / 'and' / 'mod' comparisons.
    private static readonly string[] EntC =
    {
        "entity dutc is",
        "    port (a : in std_logic; b : in std_logic; en : in boolean;",
        "          sel : in std_logic_vector(1 downto 0);",
        "          cnt : in integer; max : in integer;",
        "          y : out std_logic; z : out std_logic);",
        "end entity dutc;",
    };

    // Reject: no-else conditional, missing ';', next statement — operand before the
    // new target is a string literal.
    private static bool Test_MissingSemicolon_TrailingCond_Str() => ExpectThrows(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when sel = \"00\"",     // ERROR: missing ';'
        "    z <= b;",
        "end architecture rtl;",
    });

    // Reject: operand before the new target is an identifier (bare boolean condition).
    private static bool Test_MissingSemicolon_TrailingCond_Id() => ExpectThrows(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when en",               // ERROR: missing ';'
        "    z <= b;",
        "end architecture rtl;",
    });

    // Reject: condition ENDS in a '<=' comparison; the next statement's target is
    // still caught (it follows the operand `max`).
    private static bool Test_MissingSemicolon_TrailingCmp() => ExpectThrows(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when cnt <= max",       // ERROR: missing ';'
        "    z <= b;",
        "end architecture rtl;",
    });

    // Positive guard: a '<=' comparison in the condition must still parse.
    private static bool Test_GuardedConditionComparisonParses() => ExpectParses(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when cnt <= max else b;",
        "end architecture rtl;",
    });

    // Positive guard: a '<=' after the `and` keyword-operator must still parse.
    private static bool Test_GuardedAndBeforeComparisonParses() => ExpectParses(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when en and cnt <= max else b;",
        "end architecture rtl;",
    });

    // Positive guard: `mod` (tokenised as an identifier, the key trap) before a '<='
    // comparison must still parse.
    private static bool Test_GuardedModBeforeComparisonParses() => ExpectParses(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when cnt mod max <= cnt else b;",
        "end architecture rtl;",
    });

    // Positive guard: an indexed operand `sel(0)` before a '<=' comparison (prev = ')').
    private static bool Test_GuardedIndexOperandParses() => ExpectParses(EntC, new[]
    {
        "architecture rtl of dutc is",
        "begin",
        "    y <= a when sel(0) <= b else '0';",
        "end architecture rtl;",
    });

    // --- item B: generate-block validation (C11/C8) + nested-construct handling ---

    private static readonly string[] EntG =
    {
        "entity dutg is",
        "    port (d : in std_logic_vector(3 downto 0); clk : in std_logic;",
        "          q : out std_logic_vector(3 downto 0));",
        "end entity dutg;",
    };

    // Reject: missing ';' on a concurrent assignment inside a for-generate.
    private static bool Test_Gen_MissingSemicolon() => ExpectThrows(EntG, new[]
    {
        "architecture rtl of dutg is",
        "begin",
        "    g : for i in 0 to 3 generate",
        "        q(i) <= d(i)",          // ERROR: missing ';'
        "        q(i) <= d(i);",
        "    end generate;",
        "end architecture rtl;",
    });

    // Reject: driving an input port inside a generate (C8 now reaches generates).
    private static bool Test_Gen_AssignInputPort() => ExpectThrows(EntG, new[]
    {
        "architecture rtl of dutg is",
        "begin",
        "    g : for i in 0 to 0 generate",
        "        clk <= '0';",           // ERROR: `clk` is mode `in`
        "    end generate;",
        "end architecture rtl;",
    });

    // Reject: a with…select inside a generate with a missing ';' (the form now
    // dispatched to SelectedAssignment from generate bodies).
    private static bool Test_Gen_WithSelectMissingSemicolon() => ExpectThrows(EntG, new[]
    {
        "architecture rtl of dutg is",
        "begin",
        "    g : for i in 0 to 0 generate",
        "        with clk select q(0) <= d(0) when '0', d(1) when others",  // ERROR: missing ';'
        "        q(1) <= d(1);",
        "    end generate;",
        "end architecture rtl;",
    });

    // Positive guard: a valid with…select inside a generate (indexed target with a
    // loop variable) must still parse.
    private static bool Test_Gen_WithSelectValidParses() => ExpectParses(EntG, new[]
    {
        "architecture rtl of dutg is",
        "begin",
        "    g : for i in 0 to 3 generate",
        "        with clk select q(i) <= d(i) when '0', d(i) when others;",
        "    end generate;",
        "end architecture rtl;",
    });

    // Positive guard: a nested if-generate with a declarative part + begin (the
    // shape that previously broke the refactor) must still parse.
    private static bool Test_Gen_NestedValidParses() => ExpectParses(EntG, new[]
    {
        "architecture rtl of dutg is",
        "begin",
        "    g_out : if true generate",
        "        signal tmp : std_logic;",
        "        constant K : integer := 2;",
        "    begin",
        "        g_in : if true generate",
        "            q(0) <= d(0);",
        "        end generate g_in;",
        "    end generate g_out;",
        "end architecture rtl;",
    });

    // --- eval-time width model: C4/C5 on expression RHS (certain-width inference) ---

    private static readonly string[] EntW =
    {
        "entity dutw is",
        "    generic (W : integer := 16);",
        "    port (a4 : in std_logic_vector(3 downto 0);",
        "          b2 : in std_logic_vector(1 downto 0);",
        "          a24 : in std_logic_vector(23 downto 0);",
        "          gw : in std_logic_vector(W-1 downto 0);",
        "          y6 : out std_logic_vector(5 downto 0);",
        "          y8 : out std_logic_vector(7 downto 0);",
        "          ygw : out std_logic_vector(W-1 downto 0);",
        "          ys : out std_logic);",
        "end entity dutw;",
    };

    // Reject: a concatenation whose summed width differs from the target (6 ≠ 8).
    private static bool Test_Width_ConcatMismatch() => ExpectThrows(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    y8 <= a4 & b2;",            // ERROR: 6-bit value into an 8-bit signal
        "end architecture rtl;",
    });

    // Reject: a concatenation (always an array) assigned to a scalar target.
    private static bool Test_Width_ConcatIntoScalar() => ExpectThrows(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    ys <= a4 & b2;",            // ERROR: std_logic_vector into std_logic
        "end architecture rtl;",
    });

    // Reject: a literal-bounded slice whose width differs from the target (6 ≠ 8).
    private static bool Test_Width_SliceMismatch() => ExpectThrows(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    y8 <= a24(5 downto 0);",    // ERROR: 6-bit slice into an 8-bit signal
        "end architecture rtl;",
    });

    // Positive guard: a matching-width concatenation must parse (4 + 2 = 6).
    private static bool Test_Width_ConcatMatchParses() => ExpectParses(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    y6 <= a4 & b2;",
        "end architecture rtl;",
    });

    // Positive guard: a concat containing a NON-literal slice has unknown width →
    // must be skipped (not width-checked).
    private static bool Test_Width_NonLiteralSliceParses() => ExpectParses(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    ygw <= gw(W-3 downto 0) & \"00\";",
        "end architecture rtl;",
    });

    // Positive guard: arithmetic / conversion RHS has unknown vector width → skipped.
    private static bool Test_Width_ArithConversionParses() => ExpectParses(EntW, new[]
    {
        "architecture rtl of dutw is",
        "    signal t8 : std_logic_vector(7 downto 0);",
        "begin",
        "    y8 <= std_logic_vector(unsigned(t8) + 1);",
        "end architecture rtl;",
    });

    // --- quick-win cleanups (v1.27.0): hex-literal width + indexed C11 edge ---

    // Reject: a hex literal whose length differs from the target (12 ≠ 8).
    private static bool Test_Width_HexMismatch() => ExpectThrows(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    y8 <= x\"FFF\";",            // ERROR: 12-bit hex into an 8-bit signal
        "end architecture rtl;",
    });

    // Positive guard: a matching hex literal must parse (x"AB" = 8 bits).
    private static bool Test_Width_HexMatchParses() => ExpectParses(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    y8 <= x\"AB\";",
        "end architecture rtl;",
    });

    // Reject: a when…else with no else, missing ';', whose next statement has an
    // INDEXED target (the stray '<=' predecessor is ')').
    private static bool Test_C11_IndexedNextTarget() => ExpectThrows(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    ys <= '1' when a4 = \"0000\"",   // ERROR: missing ';'
        "    y8(0) <= '1';",
        "end architecture rtl;",
    });

    // Positive guard: an indexed comparison in a condition must still parse — the
    // ')' walk-back must not mistake `a4(0) <= b2(0)` for a new statement.
    private static bool Test_C11_IndexComparisonParses() => ExpectParses(EntW, new[]
    {
        "architecture rtl of dutw is",
        "begin",
        "    ys <= '1' when a4(0) <= b2(0) else '0';",
        "end architecture rtl;",
    });

    // --- precise-C10: type-precise cross-enum case validation ---

    private static readonly string[] EntE =
    {
        "entity dute is",
        "    port (clk : in std_logic; y : out std_logic);",
        "end entity dute;",
    };

    // Reject: a choice that is a literal of a DIFFERENT enum than the selector.
    private static bool Test_C10_CrossEnum() => ExpectThrows(EntE, new[]
    {
        "architecture rtl of dute is",
        "    type t1 is (A, B);",
        "    type t2 is (C, D);",
        "    signal s : t1;",
        "begin",
        "    process(clk) begin",
        "        case s is",
        "            when A => y <= '0';",
        "            when C => y <= '1';",      // ERROR: C is a t2 literal, not t1
        "            when others => y <= '0';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    // Reject: cross-enum literal inside a '|' alternative.
    private static bool Test_C10_CrossEnumAlt() => ExpectThrows(EntE, new[]
    {
        "architecture rtl of dute is",
        "    type t1 is (A, B);",
        "    type t2 is (C, D);",
        "    signal s : t1;",
        "begin",
        "    process(clk) begin",
        "        case s is",
        "            when A | C => y <= '0';",  // ERROR: C is a t2 literal
        "            when others => y <= '1';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    // Positive guard: '|' alternatives that are all literals of the selector type.
    private static bool Test_C10_SameEnumParses() => ExpectParses(EntE, new[]
    {
        "architecture rtl of dute is",
        "    type t1 is (A, B, Cc, Dd);",
        "    signal s : t1;",
        "begin",
        "    process(clk) begin",
        "        case s is",
        "            when A | B => y <= '0';",
        "            when others => y <= '1';",
        "        end case;",
        "    end process;",
        "end architecture rtl;",
    });

    // --- helpers ---

    private static bool ExpectThrows(string[] entityLines, string[] archLines)
    {
        try { Synthesize(entityLines, archLines); return false; } // no error → test fails
        catch (ParserException) { return true; }
        catch (Parser.Repositories.TokenStream.StreamerException) { return true; }
    }

    private static bool ExpectParses(string[] entityLines, string[] archLines)
    {
        try { Synthesize(entityLines, archLines); return true; }
        catch { return false; }
    }

    private static void Synthesize(string[] entityLines, string[] archLines)
    {
        var entity = new Entity();
        entity.Syntetize(entityLines);
        var arch = new Architecture(entity.Ports, entity.Generics);
        arch.Syntetize(archLines);
    }
}
