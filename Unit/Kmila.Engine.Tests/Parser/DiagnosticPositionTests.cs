using Parser.Models;

namespace Kmila.Engine.Tests.Parser;

/// <summary>
/// G37/G38 (v3.1.0): analysis errors keep their real line and column, so
/// <c>kmila check</c> and the editor's markers point at the offending name.
/// </summary>
public class DiagnosticPositionTests
{
    private const string Undeclared = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity e is
          port (a : in std_logic; y : out std_logic);
        end entity e;
        architecture rtl of e is
        begin
          y <= a;
          y <= foo;
        end architecture rtl;
        """;

    [Fact]
    public void An_undeclared_name_is_reported_at_its_line_and_column()
    {
        var unit = EngineFixture.AnalyzeSource(Undeclared);
        Assert.NotNull(unit.Error);
        var d = Diagnostic.FromAnyException(unit.Error!, unit.Source, "PARSE-ERROR");
        Assert.Equal(9, d.Line);
        Assert.Equal(8, d.StartColumn);   // `foo` in "  y <= foo;"
        Assert.DoesNotContain(":0", unit.Error!.Message);
    }

    private sealed class PositionedException(int line, int column) : Exception("positioned"), ISourcePosition
    {
        public int Line { get; } = line;
        public int Column { get; } = column;
    }

    [Fact]
    public void Any_exception_with_line_and_column_keeps_its_position()
    {
        var d = Diagnostic.FromAnyException(new PositionedException(4, 7), null, "BUILD-FAILED");
        Assert.Equal((4, 7, 13, "BUILD-FAILED"), (d.Line, d.StartColumn, d.EndColumn, d.Code));
    }

    [Fact]
    public void An_exception_without_position_lands_on_line_one()
    {
        var d = Diagnostic.FromAnyException(new InvalidOperationException("boom"), null, "BUILD-FAILED");
        Assert.Equal((1, 1, "boom"), (d.Line, d.StartColumn, d.Message));
    }
}
