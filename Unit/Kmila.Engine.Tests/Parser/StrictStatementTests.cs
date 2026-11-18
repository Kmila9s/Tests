using Parser.Models;

namespace Kmila.Engine.Tests.Parser;

/// <summary>
/// v3.1.3 (G25–G33): every statement GHDL 6.0.0 rejects is an analysis error at
/// GHDL's line — never a silently dropped statement — and every valid one
/// analyses. The designs are the repros of
/// <c>Documentacion/handoffs/repros_gaps_2026-11-16/parser/</c> (table in its
/// <c>EXPECTED.md</c>) and the invalid ones of <c>v313/</c>, verbatim
/// (<c>Fixtures/StrictStatements</c>).
/// </summary>
public class StrictStatementTests
{
    private static ParsedUnitResult Analyze(string file)
    {
        var unit = EngineFixture.Analyze(Path.Combine(AppContext.BaseDirectory, "Fixtures", "StrictStatements", file));
        return new(unit.Error, unit.Error is null ? null : Diagnostic.FromAnyException(unit.Error, unit.Source, "PARSE-ERROR"));
    }

    private sealed record ParsedUnitResult(Exception? Error, Diagnostic? Diagnostic);

    /// <summary>file → GHDL's error line.</summary>
    public static TheoryData<string, int> Rejected => new()
    {
        { "c1.vhdl", 22 },   // count <= count + 1; by one  (FillGap)
        { "v1.vhdl", 16 }, { "v2.vhdl", 16 }, { "v3.vhdl", 16 }, { "v4.vhdl", 16 },
        { "v5.vhdl", 17 }, { "v6.vhdl", 19 }, { "v7.vhdl", 19 }, { "v8.vhdl", 19 },
        { "v9.vhdl", 12 }, { "v10.vhdl", 12 }, { "v11.vhdl", 16 }, { "v12.vhdl", 16 },
        { "v13.vhdl", 12 }, { "v14.vhdl", 19 }, { "v15.vhdl", 16 }, { "v16.vhdl", 19 },
        { "v17.vhdl", 16 }, { "v18.vhdl", 19 },
        { "w1.vhdl", 15 }, { "w2.vhdl", 18 }, { "w3.vhdl", 21 }, { "w8.vhdl", 21 },
        { "undeclared.vhdl", 9 },   // foo(n(k)); in a generate body
        { "label_bad.vhdl", 14 },   // end if other;  (label chk)
        { "exit_bad.vhdl", 15 },    // exit l1; outside loop l1
        { "agg_undeclared.vhdl", 7 }, // (a, zz) <= v;  no declaration for "zz"
        { "agg_width.vhdl", 7 },    // (a, b) <= v(2 downto 0);  GHDL: bound check failure at line 7
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void An_invalid_statement_is_an_error_at_GHDLs_line(string file, int line)
    {
        var result = Analyze(file);
        Assert.NotNull(result.Error);
        Assert.Equal(line, result.Diagnostic!.Line);
    }

    [Theory]
    [InlineData("w0.vhdl")]   // case + for, valid base
    [InlineData("w4.vhdl")]   // lbl: count <= count + 1;
    [InlineData("w5.vhdl")]   // (a, b) <= std_logic_vector'("10");  aggregate target (G55, v3.1.4)
    [InlineData("w6.vhdl")]   // std.env.stop;
    [InlineData("w7.vhdl")]   // a <= '1' after 1 ns, '0' after 2 ns;
    [InlineData("w9.vhdl")]   // lbl: a <= not a;
    public void A_valid_statement_analyses(string file)
    {
        Assert.Null(Analyze(file).Error);
    }

    /// <summary>
    /// Valid VHDL that Kmila does not simulate yet is reported as such at its line
    /// (never dropped): the named aggregate target, which GHDL 6.0.0 itself cannot
    /// compile (internal error), so there is no reference to check it against.
    /// </summary>
    [Fact]
    public void A_named_aggregate_target_is_reported_as_not_supported_yet()
    {
        var result = Analyze("agg_named.vhdl");   // (1 => a, 0 => b) <= v;
        Assert.Contains("not supported yet", result.Error!.Message);
        Assert.Equal(7, result.Diagnostic!.Line);
    }
}
