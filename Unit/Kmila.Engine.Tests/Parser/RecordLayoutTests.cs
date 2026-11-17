using Interpreter.Models;
using Parser.Models;

namespace Kmila.Engine.Tests.Parser;

/// <summary>
/// v3.1.2 (G09, G11–G13): the record layout every field read/write, aggregate
/// and view relies on — widths from the same subtype parser as objects, fields
/// packed first-leftmost, nested records, package types — and the field codec.
/// </summary>
public class RecordLayoutTests
{
    private static Variable Signal(string vhdl, string name)
    {
        var unit = EngineFixture.AnalyzeSource(vhdl);
        unit.ThrowIfFailed();
        var entity = unit.Entities.First(e => e.Architecture != null);
        return entity.Architecture.Signals.Concat(entity.Ports)
            .First(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string Design(string declarations) => $"""
        library ieee; use ieee.std_logic_1164.all;
        entity t is port (a : in std_logic); end entity;
        architecture rtl of t is
        {declarations}
        begin
        end architecture;
        """;

    [Fact]
    public void Identifier_lists_declare_every_field_first_one_leftmost()
    {
        var p = Signal(Design("type pair_t is record lo, hi : std_logic; end record; signal p : pair_t;"), "p");
        Assert.Equal(["lo", "hi"], p.Layout!.Fields.Select(f => f.Name));
        Assert.Equal([0, 1], p.Layout.Fields.Select(f => f.Offset));
        Assert.Equal(2, p.Size);
    }

    [Fact]
    public void Field_widths_follow_their_subtype()
    {
        var c = Signal(Design("""
            constant W : integer := 4;
            type state_t is (idle, run, done);
            type ctl_t is record
              cnt : integer; lim : integer range 0 to 15; neg : integer range -4 to 3;
              s : state_t; ok : boolean; d : std_logic_vector(W-1 downto 0);
            end record;
            signal c : ctl_t;
            """), "c");
        var f = c.Layout!.Fields.ToDictionary(x => x.Name);
        Assert.Equal((32, FieldKind.Integer, true), (f["cnt"].Width, f["cnt"].Kind, f["cnt"].Signed));
        Assert.Equal((4, false), (f["lim"].Width, f["lim"].Signed));
        Assert.True(f["neg"].Signed);
        Assert.Equal((2, FieldKind.Enum), (f["s"].Width, f["s"].Kind));
        Assert.Equal(["idle", "run", "done"], f["s"].Shape.EnumLiterals);
        Assert.Equal((1, FieldKind.Boolean), (f["ok"].Width, f["ok"].Kind));
        Assert.Equal((4, FieldKind.Vector), (f["d"].Width, f["d"].Kind));
        Assert.Equal(c.Layout.Fields.Sum(x => x.Width), c.Size);
    }

    [Fact]
    public void Nested_records_flatten_to_dotted_leaves_with_absolute_offsets()
    {
        var q = Signal(Design("""
            type pair_t is record lo, hi : std_logic; end record;
            type quad_t is record a : pair_t; b : pair_t; end record;
            signal q : quad_t;
            """), "q");
        Assert.Equal([("a.lo", 0), ("a.hi", 1), ("b.lo", 2), ("b.hi", 3)],
            q.Layout!.Leaves().Select(l => (l.Path, l.Offset)));
    }

    [Fact]
    public void Package_records_type_ports_and_publish_enum_literals()
    {
        var ci = Signal("""
            library ieee; use ieee.std_logic_1164.all;
            package p is
              type mode_t is (m_off, m_on);
              type cfg_t is record en : std_logic; mode : mode_t; d : std_logic_vector(3 downto 0); end record;
            end package;
            library ieee; use ieee.std_logic_1164.all;
            use work.p.all;
            entity t is port (ci : in cfg_t; y : out std_logic); end entity;
            architecture rtl of t is begin y <= ci.en; end architecture;
            """, "ci");
        Assert.Equal("cfg_t", ci.Layout!.TypeName);
        Assert.Equal(6, ci.Size);
    }

    [Fact]
    public void Arrays_of_records_carry_the_element_layout_and_their_bounds()
    {
        var m = Signal(Design("""
            constant N : integer := 4;
            type pair_t is record lo, hi : std_logic; end record;
            type parr_t is array (1 to N) of pair_t;
            signal m : parr_t;
            """), "m");
        Assert.Equal((2, 4, 8), (m.ElementSize, m.ArrayLength, m.Size));
        Assert.Equal((1, 4, false), (m.LeftBound, m.RightBound, m.IsDownto));
        Assert.NotNull(m.ElementShape?.Layout);
    }

    [Fact]
    public void Field_codec_round_trips_integers_enums_and_booleans()
    {
        var shape = new Variable { Type = Constants.DATATYPES.INTEGER, EnumLiterals = ["idle", "run", "done"] };
        var e = new FieldSlot("s", 0, 2, FieldKind.Enum, shape);
        Assert.Equal("10", FieldCodec.Encode(e, "done"));
        Assert.Equal("done", FieldCodec.Decode(e, "10", "s").Value);

        var i = new FieldSlot("n", 0, 4, FieldKind.Integer, new Variable(), Signed: true);
        Assert.Equal("1101", FieldCodec.Encode(i, -3));
        Assert.Equal(-3L, FieldCodec.Decode(i, "1101", "n").Value);

        var b = new FieldSlot("ok", 0, 1, FieldKind.Boolean, new Variable());
        Assert.Equal("1", FieldCodec.Encode(b, true));
        Assert.Equal(false, FieldCodec.Decode(b, "0", "ok").Value);
    }
}
