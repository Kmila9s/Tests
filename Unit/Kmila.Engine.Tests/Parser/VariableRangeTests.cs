using Parser.Models;
using Xunit;

namespace Parser.Tests;

/// <summary>
/// Tests for the declared-range (to/downto + bounds) support on
/// <see cref="Variable"/> (2026-10-06 fix). A vector's declared range is now
/// recorded so index/slice resolution honours ascending (<c>to</c>) ranges and
/// non-zero low bounds instead of assuming <c>(Size-1 downto 0)</c>. When no
/// range is recorded the helpers fall back to the conventional MSB-first form,
/// which must stay byte-identical to the legacy behaviour.
/// </summary>
public class VariableRangeTests
{
    [Fact]
    public void NoDeclaredRange_FallsBackToMsbFirst()
    {
        var v = new Variable("v", "1010", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        Assert.False(v.HasDeclaredRange);
        Assert.Equal(3, v.IndexToOffset(0, 4)); // bit 0 -> rightmost
        Assert.Equal(0, v.IndexToOffset(3, 4)); // bit 3 -> leftmost
    }

    [Fact]
    public void DescendingZeroBased_MatchesFallback()
    {
        var v = new Variable("v", "1010", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        v.SetDeclaredRange(3, 0, isDownto: true);
        Assert.True(v.HasDeclaredRange);
        Assert.Equal(3, v.IndexToOffset(0, 4));
        Assert.Equal(0, v.IndexToOffset(3, 4));
    }

    [Fact]
    public void Ascending_MapsLeftElementToPositionZero()
    {
        // (0 to 3): index 0 is the leftmost (position 0), index 3 the rightmost.
        var v = new Variable("v", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        v.SetDeclaredRange(0, 3, isDownto: false);
        Assert.Equal(0, v.IndexToOffset(0, 4)); // was wrongly 3 before the fix
        Assert.Equal(3, v.IndexToOffset(3, 4));
    }

    [Fact]
    public void OffsetDownto_SubtractsLowBound()
    {
        // (7 downto 4): index 7 is leftmost (position 0), index 4 rightmost.
        var v = new Variable("v", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        v.SetDeclaredRange(7, 4, isDownto: true);
        Assert.Equal(0, v.IndexToOffset(7, 4));
        Assert.Equal(3, v.IndexToOffset(4, 4));
    }

    [Theory]
    // Ascending slice (1 to 2) of "1100" on (0 to 3) -> positions 1..2 = "10".
    [InlineData(0, 3, false, 2, 1, 1, 2)]
    // Offset-downto slice (6 downto 5) of a 4-bit (7 downto 4) -> positions 1..2.
    [InlineData(7, 4, true, 6, 5, 1, 2)]
    // Conventional (7 downto 0) slice (5 downto 2) -> positions 2..5.
    [InlineData(7, 0, true, 5, 2, 2, 4)]
    public void SliceToSpan_HonoursDeclaredRange(
        int left, int right, bool downto, int hi, int lo, int expectStart, int expectWidth)
    {
        int len = System.Math.Abs(left - right) + 1;
        var v = new Variable("v", new string('0', len), Constants.DATATYPES.STD_LOGIC_VECTOR, len);
        v.SetDeclaredRange(left, right, downto);
        var (startPos, width) = v.SliceToSpan(hi, lo, len);
        Assert.Equal(expectStart, startPos);
        Assert.Equal(expectWidth, width);
    }
}
