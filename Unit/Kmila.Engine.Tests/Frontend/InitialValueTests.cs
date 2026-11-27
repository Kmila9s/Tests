using Interpreter.Ir.Vhdl;
using Interpreter.Models;
using Interpreter.Services;

namespace Kmila.Engine.Tests.Frontend;

/// <summary>
/// v3.1.5 (G39, G47, G48): the LRM initial value of every object declared without <c>:=</c>
/// (<c>T'left</c>), port defaults, the didactic seeding, and VHDL integer arithmetic.
/// </summary>
public class InitialValueTests
{
    private static Entity Parse(string decls, string ports = "clk : in std_logic; k : in std_logic := '1'")
    {
        var unit = EngineFixture.AnalyzeSource($"""
            library ieee;
            use ieee.std_logic_1164.all;
            entity e is
              port ({ports});
            end entity e;
            architecture rtl of e is
              type state_t is (idle, run, done);
              {decls}
            begin
            end architecture rtl;
            """);
        unit.ThrowIfFailed();
        return unit.Entities.Single(e => e.Architecture != null);
    }

    [Theory]
    [InlineData("signal s : std_logic;", "U")]
    [InlineData("signal v : std_logic_vector(3 downto 0);", "UUUU")]
    [InlineData("signal b : bit;", "0")]
    [InlineData("signal bv : bit_vector(2 downto 0);", "000")]
    [InlineData("signal i : integer;", "-2147483648")]
    [InlineData("signal r : integer range -4 to 3;", "-4")]
    [InlineData("signal n : natural;", "0")]
    [InlineData("signal p : positive;", "1")]
    [InlineData("signal st : state_t;", "idle")]
    [InlineData("signal f : boolean;", "False")]
    [InlineData("signal w : std_logic := '1';", "1")]
    public void Objects_start_at_their_LRM_value(string decl, string expected)
    {
        var entity = Parse(decl);
        string name = decl.Split(' ')[1];
        var v = entity.Architecture!.Signals.Single(s => s.Name == name);
        Assert.Equal(expected, v.Value.ToString());
    }

    [Fact]
    public void Ports_take_their_default_or_T_left()
    {
        var entity = Parse("signal s : std_logic;");
        Assert.Equal("U", entity.Ports.Single(p => p.Name == "clk").Value.ToString());
        Assert.Equal("1", entity.Ports.Single(p => p.Name == "k").Value.ToString());
    }

    [Fact]
    public void Didactic_policy_seeds_zero_without_touching_initializers()
    {
        var entity = Parse("signal v : std_logic_vector(3 downto 0); signal w : std_logic := '1'; signal r : integer range 1 to 4; signal en : std_logic;");
        InitialValueSeeder.Apply(InitialValuePolicy.Didactic, InitialValueSeeder.ObjectsOf(VhdlDesignAdapter.From(entity)), new HashSet<global::Parser.Models.Variable>());
        var sig = entity.Architecture!.Signals.ToDictionary(s => s.Name);
        Assert.Equal("0000", sig["v"].Value.ToString());
        Assert.Equal("1", sig["w"].Value.ToString());
        Assert.Equal("1", sig["r"].Value.ToString()); // 0 clamped into 1 to 4
        Assert.Equal("1", sig["en"].Value.ToString());
    }

    [Fact]
    public void Lrm_policy_leaves_every_object_alone()
    {
        var entity = Parse("signal v : std_logic_vector(3 downto 0);");
        InitialValueSeeder.Apply(InitialValuePolicy.Lrm, InitialValueSeeder.ObjectsOf(VhdlDesignAdapter.From(entity)), new HashSet<global::Parser.Models.Variable>());
        Assert.Equal("UUUU", entity.Architecture!.Signals.Single(s => s.Name == "v").Value.ToString());
    }

    [Theory]
    [InlineData("mod", -7, 3, 2)]
    [InlineData("mod", 7, -3, -2)]
    [InlineData("mod", -7, -3, -1)]
    [InlineData("rem", -7, 3, -1)]
    [InlineData("rem", 7, -3, 1)]
    [InlineData("/", -7, 2, -3)]
    public void Integer_arithmetic_is_VHDLs(string op, long a, long b, long expected) =>
        Assert.Equal(expected, IntegerArithmetic.Apply(op, a, b));

    [Theory]
    [InlineData("/")]
    [InlineData("mod")]
    [InlineData("rem")]
    public void Division_by_zero_is_a_run_time_error(string op) =>
        Assert.Equal(IntegerArithmetic.DivisionByZero,
            Assert.Throws<SimulationRuntimeException>(() => IntegerArithmetic.Apply(op, 1, 0)).Reason);
}
