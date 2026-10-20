using Kmila.Shared.Services;

namespace Kmila.Shared.Tests;

// Round-trip = Build → Emit. Idempotence isn't the goal — VHDL formatting
// is normalised on emit. What matters is that the produced VHDL preserves
// the *observable* content the student wrote (identifiers, types, ops,
// control flow) so the simulator sees the same design.
public class RoundTripTests
{
    private static string Roundtrip(string vhdl) =>
        new VhdlBlockEmitter().Emit(new VhdlBlockBuilder().Build(vhdl));

    private static string Squish(string s)
    {
        // Collapse whitespace and lowercase — normalisation for
        // comparing "same content, different formatting".
        var sb = new System.Text.StringBuilder(s.Length);
        bool prevWs = true;
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!prevWs) sb.Append(' ');
                prevWs = true;
            }
            else
            {
                sb.Append(c);
                prevWs = false;
            }
        }
        return sb.ToString().Trim();
    }

    [Fact]
    public void RoundTrip_TrivialPassthrough_PreservesEntityAndAssignment()
    {
        const string src = """
            entity foo is port (a : in std_logic; y : out std_logic); end foo;
            architecture rtl of foo is begin y <= a; end rtl;
            """;
        var actual = Squish(Roundtrip(src));
        Assert.Contains("entity foo is", actual);
        Assert.Contains("y <= a", actual);
    }

    [Fact]
    public void RoundTrip_ClockedProcess_PreservesRisingEdgeAndAssignment()
    {
        const string src = """
            entity foo is port (clk : in std_logic; q : out std_logic); end foo;
            architecture rtl of foo is begin
              process (clk) begin
                if rising_edge(clk) then
                  q <= '1';
                end if;
              end process;
            end rtl;
            """;
        var actual = Squish(Roundtrip(src));
        Assert.Contains("process", actual);
        Assert.Contains("rising_edge(clk)", actual);
        Assert.Contains("q <= '1'", actual);
    }

    [Fact]
    public void RoundTrip_CaseWithVectorLiteral_PreservesShapeAndLiterals()
    {
        const string src = """
            entity mux is
              port (sel : in std_logic_vector(1 downto 0); y : out std_logic_vector(3 downto 0));
            end mux;
            architecture rtl of mux is begin
              process (sel) begin
                case sel is
                  when "00" => y <= "0001";
                  when others => y <= "0000";
                end case;
              end process;
            end rtl;
            """;
        var actual = Squish(Roundtrip(src));
        Assert.Contains("case sel is", actual);
        Assert.Contains("\"0001\"", actual);
        Assert.Contains("\"0000\"", actual);
        Assert.Contains("when others", actual);
    }

    [Fact]
    public void RoundTrip_BinaryOp_PreservesOperator()
    {
        const string src = """
            entity g is port (a : in std_logic; b : in std_logic; y : out std_logic); end g;
            architecture rtl of g is begin y <= a and b; end rtl;
            """;
        var actual = Squish(Roundtrip(src));
        Assert.Contains("a and b", actual);
    }

    [Fact]
    public void RoundTrip_FallingEdge_PreservesCall()
    {
        const string src = """
            entity ff is port (clk : in std_logic; q : out std_logic); end ff;
            architecture rtl of ff is begin
              process (clk) begin
                if falling_edge(clk) then q <= '0'; end if;
              end process;
            end rtl;
            """;
        var actual = Squish(Roundtrip(src));
        Assert.Contains("falling_edge(clk)", actual);
    }

    // The emit path must at minimum keep the entity/architecture skeleton
    // even when everything in the body degrades to raw fallback.
    [Fact]
    public void RoundTrip_HeavilyMalformedBody_StillEmitsSkeletonOrEmpty()
    {
        const string src = """
            entity bad is port (a : in std_logic); end bad;
            architecture rtl of bad is begin
              ??? garbage that doesn't parse ???
            end rtl;
            """;
        var actual = Roundtrip(src);
        // Never throws; result is either empty (rejected) or contains
        // the entity — never a partial mangle that would confuse the
        // downstream simulator.
        if (!string.IsNullOrEmpty(actual))
        {
            Assert.Contains("entity bad is", Squish(actual));
        }
    }
}
