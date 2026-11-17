using Debugger.Repositories.Lint;

namespace Kmila.Engine.Tests.Parser;

/// <summary>
/// GAP-LINT-001 (v3.0.6): the structural lint rules must stay silent on valid
/// VHDL — statements continued on the next line, the last element of a
/// port/generic list, nested <c>end if</c>/<c>end process</c> — and still
/// point at real mistakes.
/// </summary>
public class LintRulesTests
{
    private static List<string> Codes(string source) => VhdlLinter.Run(source).Select(d => $"{d.Code}@{d.Line}").ToList();

    /// <summary>1-based line of the first line of <paramref name="source"/> containing <paramref name="text"/>.</summary>
    private static int LineOf(string source, string text) =>
        source.Split('\n').Select((l, i) => (l, i)).First(x => x.l.Contains(text)).i + 1;

    [Theory]
    [MemberData(nameof(ParserCorpusTests.CorpusFiles), MemberType = typeof(ParserCorpusTests))]
    public void Valid_corpus_file_has_no_lint_warnings(string file) =>
        Assert.Empty(Codes(File.ReadAllText(EngineFixture.FixturePath("ParserCorpus", file))));

    [Fact]
    public void Missing_port_semicolon_is_reported_on_its_line() =>
        Assert.Equal(["VHD-MISSING-SEMICOLON@14"],
            Codes(File.ReadAllText(EngineFixture.FixturePath("ParserCorpus", "test_negative_missing_port_semi.vhdl"))));

    private const string Header = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity e is
          port (clk : in std_logic; q : out std_logic);
        end entity e;
        """;

    [Fact]
    public void Statements_continued_on_the_next_line_are_not_flagged() => Assert.Empty(Codes(Header + """

        architecture rtl of e is
          constant K : std_logic_vector(7 downto 0) :=
            x"5A";
          signal s : std_logic;
        begin
          s <= clk when K(0) = '0'
               else not clk;
          q <= s;
        end architecture rtl;
        """));

    [Fact]
    public void A_statement_without_semicolon_is_reported()
    {
        var src = Header + """

            architecture rtl of e is
            begin
              q <= clk
            end architecture rtl;
            """;
        Assert.Equal([$"VHD-MISSING-SEMICOLON@{LineOf(src, "q <= clk")}"], Codes(src));
    }

    [Fact]
    public void Nested_blocks_balance() => Assert.Empty(Codes(Header + """

        architecture rtl of e is
          type state_t is (idle, run);
          signal st : state_t := idle;
        begin
          p : process (clk)
          begin
            if rising_edge(clk) then
              case st is
                when idle => st <= run;
                when others => st <= idle;
              end case;
              for i in 0 to 3 loop
                null;
              end loop;
            end if;
          end process p;
          q <= '1' when st = run else '0';
        end architecture rtl;
        """));

    [Fact]
    public void A_missing_end_process_is_reported_where_the_process_opens()
    {
        var src = Header + """

            architecture rtl of e is
            begin
              process (clk)
              begin
                q <= clk;
            end architecture rtl;
            """;
        var codes = Codes(src);
        Assert.Contains($"VHD-BLOCK-BALANCE@{LineOf(src, "end architecture")}", codes); // closes the process
        Assert.Contains($"VHD-BLOCK-BALANCE@{LineOf(src, "architecture rtl")}", codes); // never closed
    }

    [Fact]
    public void Mismatched_end_keyword_is_reported()
    {
        var src = Header + """

            architecture rtl of e is
            begin
              process (clk)
              begin
                if clk = '1' then
                  q <= '1';
              end process;
              end if;
            end architecture rtl;
            """;
        Assert.Contains($"VHD-BLOCK-BALANCE@{LineOf(src, "end process")}", Codes(src));
    }

    [Fact]
    public void Architecture_without_begin_and_duplicate_begin_are_reported()
    {
        var noBegin = Header + """

            architecture rtl of e is
              signal s : std_logic;
            end architecture rtl;
            """;
        Assert.Equal([$"VHD-BLOCK-BALANCE@{LineOf(noBegin, "architecture rtl")}"], Codes(noBegin));

        var twoBegins = Header + """

            architecture rtl of e is
            begin
            begin
              q <= clk;
            end architecture rtl;
            """;
        Assert.Equal([$"VHD-BLOCK-BALANCE@{LineOf(twoBegins, "begin") + 1}"], Codes(twoBegins));
    }

    [Fact]
    public void Instantiations_declarations_and_comments_do_not_open_blocks() => Assert.Empty(Codes(Header + """

        architecture rtl of e is
          component c is port (a : in std_logic); end component;
          function f (x : std_logic) return std_logic;   -- declaration only
        begin
          u1 : entity work.e port map (clk => clk, q => open);
          u2 : component c port map (a => clk);
          q <= '1'; -- if this were code it would open a block: "end if; begin"
        end architecture rtl;
        """));
}
