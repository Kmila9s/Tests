using Interpreter.Models;
using Kmila.Shared.Services.VisualRun;
using Parser.Models;
using Xunit;

namespace Kmila.Shared.Tests;

/// <summary>
/// Regression tests for the declared-range (to/downto + bounds) fix (2026-10).
/// The engine stores vectors MSB-first in declaration order; index/slice
/// resolution must honour the declared range instead of assuming
/// (Size-1 downto 0). Validated behaviours below mirror GHDL ground truth.
/// </summary>
public class RangeAwareIndexingTests
{
    // --- Variable.IndexToOffset ------------------------------------------

    [Fact]
    public void IndexToOffset_NoDeclaredRange_FallsBackToMsbFirst()
    {
        // Byte-identical to the legacy len-1-index behaviour.
        var v = new Variable("v", "1010", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        Assert.False(v.HasDeclaredRange);
        Assert.Equal(3, v.IndexToOffset(0, 4)); // bit 0 -> rightmost
        Assert.Equal(0, v.IndexToOffset(3, 4)); // bit 3 -> leftmost
    }

    [Fact]
    public void IndexToOffset_DescendingZeroBased_MatchesFallback()
    {
        var v = new Variable("v", "1010", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        v.SetDeclaredRange(3, 0, isDownto: true);
        Assert.Equal(3, v.IndexToOffset(0, 4));
        Assert.Equal(0, v.IndexToOffset(3, 4));
    }

    [Fact]
    public void IndexToOffset_Ascending_MapsLeftToPositionZero()
    {
        // (0 to 3): index 0 is the leftmost (position 0), index 3 the rightmost.
        var v = new Variable("v", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        v.SetDeclaredRange(0, 3, isDownto: false);
        Assert.Equal(0, v.IndexToOffset(0, 4)); // was wrongly 3 before the fix
        Assert.Equal(3, v.IndexToOffset(3, 4));
    }

    [Fact]
    public void IndexToOffset_OffsetDownto_SubtractsLowBound()
    {
        // (7 downto 4): index 7 is leftmost (position 0), index 4 rightmost.
        var v = new Variable("v", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        v.SetDeclaredRange(7, 4, isDownto: true);
        Assert.Equal(0, v.IndexToOffset(7, 4));
        Assert.Equal(3, v.IndexToOffset(4, 4));
    }

    // --- DeferredIndex single-bit reads ----------------------------------

    [Theory]
    [InlineData(0, "1")] // asc(0) = leftmost of "1000"
    [InlineData(3, "0")] // asc(3) = rightmost
    public void DeferredIndex_AscendingSingleBit_MatchesVhdl(int idx, string expected)
    {
        var src = new Variable("asc", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        src.SetDeclaredRange(0, 3, isDownto: false);
        var di = new DeferredIndex(src, idx, idx, isSlice: false);
        Assert.Equal(expected, di.Evaluate().Value?.ToString());
    }

    [Theory]
    [InlineData(7, "1")] // dn(7) high bit of "1000" on (7 downto 4)
    [InlineData(4, "0")] // dn(4) low bit
    public void DeferredIndex_OffsetDownToSingleBit_MatchesVhdl(int idx, string expected)
    {
        var src = new Variable("dn", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        src.SetDeclaredRange(7, 4, isDownto: true);
        var di = new DeferredIndex(src, idx, idx, isSlice: false);
        Assert.Equal(expected, di.Evaluate().Value?.ToString());
    }

    // --- DeferredIndex slice reads ---------------------------------------

    [Fact]
    public void DeferredIndex_AscendingSlice_KeepsDeclarationOrder()
    {
        // asc = "1100" on (0 to 3). Slice (1 to 2) -> elements 1,2 -> "10".
        var src = new Variable("asc", "1100", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        src.SetDeclaredRange(0, 3, isDownto: false);
        // Operation normalises high=larger(2), low=smaller(1).
        var di = new DeferredIndex(src, high: 2, low: 1, isSlice: true);
        Assert.Equal("10", di.Evaluate().Value?.ToString());
    }

    [Fact]
    public void DeferredIndex_DownToZeroSlice_Unchanged()
    {
        // Sanity: conventional (7 downto 0) slice still works as before.
        var src = new Variable("v", "10110010", Constants.DATATYPES.STD_LOGIC_VECTOR, 8);
        src.SetDeclaredRange(7, 0, isDownto: true);
        var di = new DeferredIndex(src, high: 5, low: 2, isSlice: true);
        // positions 2..5 of "10110010" = "1100"
        Assert.Equal("1100", di.Evaluate().Value?.ToString());
    }

    // --- App-layer SliceHelper range-aware overloads ----------------------

    [Fact]
    public void SliceHelper_LegacyExtract_UnchangedForBareString()
    {
        // No port -> legacy downto-0 behaviour preserved.
        Assert.Equal("0", SliceHelper.Extract("1010", new BitSlice(0, 0)));
        Assert.Equal("101", SliceHelper.Extract("1010", new BitSlice(3, 1)));
    }

    [Fact]
    public void SliceHelper_Extract_OffsetDownToPort()
    {
        // Port (7 downto 4), value "1000": bit 7 -> "1", bit 4 -> "0".
        var port = new Variable("dn", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        port.SetDeclaredRange(7, 4, isDownto: true);
        Assert.Equal("1", SliceHelper.Extract("1000", new BitSlice(7, 7), port));
        Assert.Equal("0", SliceHelper.Extract("1000", new BitSlice(4, 4), port));
    }

    [Fact]
    public void SliceHelper_Extract_AscendingPort()
    {
        // Port (0 to 3), value "1000": bit 0 -> "1" (leftmost), bit 3 -> "0".
        var port = new Variable("asc", "1000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        port.SetDeclaredRange(0, 3, isDownto: false);
        Assert.Equal("1", SliceHelper.Extract("1000", new BitSlice(0, 0), port));
        Assert.Equal("0", SliceHelper.Extract("1000", new BitSlice(3, 3), port));
    }

    [Fact]
    public void SliceHelper_Compose_OffsetDownToPort_RoundTrips()
    {
        // Compose onto (7 downto 4): setting bit 7 lands at the MSB position.
        var port = new Variable("dn", "0000", Constants.DATATYPES.STD_LOGIC_VECTOR, 4);
        port.SetDeclaredRange(7, 4, isDownto: true);
        var contrib = new[] { new KeyValuePair<BitSlice, string>(new BitSlice(7, 7), "1") };
        Assert.Equal("1000", SliceHelper.Compose(4, contrib, port));
    }
}
