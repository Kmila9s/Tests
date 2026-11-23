using Parser.Models;
using static Kmila.Shared.Tests.Engine.EngineHarness;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.9 G82: an error inside an instantiated child names the child's file and the
/// line the user wrote, not the line of the elaborated (flattened) text — as GHDL
/// (<c>hijo.vhdl:14</c>). Both a run-time error and a parse error of the child.
/// </summary>
public class V319Tests
{
    private const string Child = """
        library ieee;
        use ieee.std_logic_1164.all;
        use ieee.numeric_std.all;

        entity hijo is
          port (sel : in  std_logic_vector(2 downto 0);
                y   : out std_logic);
        end entity;

        architecture rtl of hijo is
          signal v : std_logic_vector(3 downto 0) := "1010";
        begin
          -- line 14: index out of range when sel >= 4
          y <= v(to_integer(unsigned(sel)));
        end architecture;
        """;

    private const string Testbench = """
        library ieee;
        use ieee.std_logic_1164.all;

        entity tb is
        end entity;

        architecture t of tb is
          signal sel : std_logic_vector(2 downto 0) := "000";
          signal y : std_logic;
        begin
          u: entity work.hijo port map (sel => sel, y => y);
          process
          begin
            wait for 1 ns; sel <= "101";
            wait;
          end process;
        end architecture;
        """;

    private static Dictionary<string, string> Design(string child) => new() { ["hijo.vhdl"] = child, ["tb.vhdl"] = Testbench };

    [Fact]
    public async Task Runtime_error_in_a_child_names_its_file_and_line()
    {
        var run = await RunFilesAsync(Design(Child), "tb", 5, order: ["hijo.vhdl", "tb.vhdl"]);
        Assert.NotNull(run.EngineError);
        Assert.Contains("out of bounds (3 downto 0) at Line 14 in hijo.vhdl", run.EngineError);
    }

    [Fact]
    public async Task Parse_error_in_a_child_names_its_file_and_line()
    {
        var child = Child.Replace("y <= v(to_integer(unsigned(sel)));", "y <= w(0);");
        var error = await Assert.ThrowsAnyAsync<ParserException>(() => RunFilesAsync(Design(child), "tb", 5, order: ["hijo.vhdl", "tb.vhdl"]));
        Assert.Equal(14, error.Line);
        Assert.EndsWith("hijo.vhdl", error.File);
        Assert.Contains("in hijo.vhdl", error.Message);
    }
}
