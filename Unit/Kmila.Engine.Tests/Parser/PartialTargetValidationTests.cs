namespace Kmila.Engine.Tests.Parser;

/// <summary>
/// v3.1.1 (G17, G49): a part target with literal indices is checked at
/// analysis — an index outside the declared range and a slice whose width
/// differs from its value are errors at the assignment's line, not a silent
/// no-op at run time.
/// </summary>
public class PartialTargetValidationTests
{
    private static string Design(string statement) => $"""
        library ieee;
        use ieee.std_logic_1164.all;
        entity e is
          port (a, b : in std_logic; y : out std_logic_vector(3 downto 0));
        end entity e;
        architecture rtl of e is
        begin
          {statement}
        end architecture rtl;
        """;

    [Theory]
    [InlineData("y(5) <= a;", "out of range")]
    [InlineData("y(4 downto 3) <= a & b;", "out of range")]
    [InlineData("y(3 downto 1) <= a & b;", "width mismatch")]
    public void A_bad_literal_part_target_is_an_analysis_error(string statement, string message)
    {
        var unit = EngineFixture.AnalyzeSource(Design(statement));
        Assert.NotNull(unit.Error);
        Assert.Contains(message, unit.Error!.Message);
        Assert.Contains("Line 8", unit.Error.Message);   // the statement's line
    }

    [Theory]
    [InlineData("y(3 downto 2) <= a & b;")]
    [InlineData("y(0) <= a;")]
    public void A_valid_part_target_analyses(string statement)
    {
        Assert.Null(EngineFixture.AnalyzeSource(Design(statement)).Error);
    }
}
