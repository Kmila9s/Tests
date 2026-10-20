using Kmila.Shared.Services.VisualRun;

namespace Kmila.Shared.Tests;

// Ring 5 (2026-07-14): write-side slice merging tests. Parse + Extract
// already had implicit coverage via VisualRunCoordinator's read path;
// the new Compose and DetectOverlap surface warrants direct unit
// coverage because fan-in packing math is easy to get wrong.

public class SliceHelperTests
{
    [Fact]
    public void Parse_wholePortName_returnsNullSlice()
    {
        var (name, slice) = SliceHelper.Parse("count");
        Assert.Equal("count", name);
        Assert.Null(slice);
    }

    [Fact]
    public void Parse_singleBit_returnsEqualHighLow()
    {
        var (name, slice) = SliceHelper.Parse("count[3]");
        Assert.Equal("count", name);
        Assert.NotNull(slice);
        Assert.Equal(3, slice!.Value.High);
        Assert.Equal(3, slice.Value.Low);
    }

    [Fact]
    public void Parse_range_returnsHighLow()
    {
        var (name, slice) = SliceHelper.Parse("bus[7:4]");
        Assert.Equal("bus", name);
        Assert.NotNull(slice);
        Assert.Equal(7, slice!.Value.High);
        Assert.Equal(4, slice.Value.Low);
    }

    [Fact]
    public void Parse_reversedRange_normalized()
    {
        var (_, slice) = SliceHelper.Parse("bus[0:3]");
        Assert.NotNull(slice);
        Assert.Equal(3, slice!.Value.High);
        Assert.Equal(0, slice.Value.Low);
    }

    [Fact]
    public void Extract_wholePort_returnsFullValue()
    {
        Assert.Equal("1010", SliceHelper.Extract("1010", null));
    }

    [Fact]
    public void Extract_singleBit_picksLsbNumberedChar()
    {
        // 4-bit "1010" — bit 0 = '0', bit 3 = '1'
        Assert.Equal("0", SliceHelper.Extract("1010", new BitSlice(0, 0)));
        Assert.Equal("1", SliceHelper.Extract("1010", new BitSlice(3, 3)));
    }

    [Fact]
    public void Extract_range_returnsMsbFirstSubstring()
    {
        Assert.Equal("101", SliceHelper.Extract("1010", new BitSlice(3, 1)));
        Assert.Equal("10", SliceHelper.Extract("1010", new BitSlice(1, 0)));
    }

    [Fact]
    public void Compose_fourSingleBits_packsCorrectly()
    {
        // Four keypad col outputs → 4-bit cols_in port.
        // col0='1', col1='0', col2='1', col3='0' → "0101" (MSB=col3)
        var contribs = new Dictionary<BitSlice, string>
        {
            [new BitSlice(0, 0)] = "1",
            [new BitSlice(1, 1)] = "0",
            [new BitSlice(2, 2)] = "1",
            [new BitSlice(3, 3)] = "0",
        };
        Assert.Equal("0101", SliceHelper.Compose(4, contribs));
    }

    [Fact]
    public void Compose_undrivenBits_defaultZero()
    {
        // Only bit 0 driven — bits 1..3 default to '0'.
        var contribs = new Dictionary<BitSlice, string>
        {
            [new BitSlice(0, 0)] = "1",
        };
        Assert.Equal("0001", SliceHelper.Compose(4, contribs));
    }

    [Fact]
    public void Compose_twoDisjointRanges_packsEachHalf()
    {
        // 8-bit port: [7:4] = "1010", [3:0] = "1100" → "10101100"
        var contribs = new Dictionary<BitSlice, string>
        {
            [new BitSlice(7, 4)] = "1010",
            [new BitSlice(3, 0)] = "1100",
        };
        Assert.Equal("10101100", SliceHelper.Compose(8, contribs));
    }

    [Fact]
    public void Compose_shortContribution_leftPadded()
    {
        // Widget wrote "1" for a 4-bit slice → pads to "0001".
        // "0001" placed at bits [3:0] of an 8-bit port → "00000001".
        var contribs = new Dictionary<BitSlice, string>
        {
            [new BitSlice(3, 0)] = "1",
        };
        Assert.Equal("00000001", SliceHelper.Compose(8, contribs));
    }

    [Fact]
    public void Compose_zeroWidth_returnsEmpty()
    {
        Assert.Equal("", SliceHelper.Compose(0, new Dictionary<BitSlice, string>()));
    }

    [Fact]
    public void DetectOverlap_disjointSlices_returnsEmpty()
    {
        var slices = new[] { new BitSlice(7, 4), new BitSlice(3, 0) };
        Assert.Empty(SliceHelper.DetectOverlap(slices));
    }

    [Fact]
    public void DetectOverlap_intersecting_flagged()
    {
        // [7:4] and [5:2] overlap at bits 4..5
        var slices = new[] { new BitSlice(7, 4), new BitSlice(5, 2) };
        var collisions = SliceHelper.DetectOverlap(slices);
        Assert.Single(collisions);
    }

    [Fact]
    public void DetectOverlap_identicalSingleBits_flagged()
    {
        var slices = new[] { new BitSlice(3, 3), new BitSlice(3, 3) };
        Assert.Single(SliceHelper.DetectOverlap(slices));
    }
}
