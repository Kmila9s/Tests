using Interpreter.Ir;
using Interpreter.Ir.Vhdl;
using Kmila.Frontend;
using Xunit;

namespace Kmila.Engine.Tests.Ir;

/// <summary>
/// The VHDL front-end's lowering to the language-neutral IR (Fase 2, v3.2.1): every object
/// with its kind, type and shape, every concurrent unit (generates unrolled) with its
/// sensitivity, and the transitional binding to the parsed objects that hold the values.
/// </summary>
public class VhdlDesignAdapterTests
{
    private const string Design = """
        library ieee;
        use ieee.std_logic_1164.all;
        use ieee.numeric_std.all;
        entity dut is
          generic (N : integer := 4);
          port (clk : in std_logic;
                v   : in std_logic_vector(7 downto 0);
                s   : in signed(3 downto 0);
                io  : inout std_logic;
                y   : out unsigned(0 to 3);
                ok  : out boolean);
        end entity;
        architecture rtl of dut is
          type state_t is (idle, run, done);
          type pair_t is record lo : std_logic; hi : std_logic_vector(1 downto 0); end record;
          type mem_t is array (0 to 3) of std_logic_vector(7 downto 0);
          constant K : integer := 3;
          signal st  : state_t;
          signal p   : pair_t;
          signal mem : mem_t;
          signal cnt : integer range 0 to 15;
          signal a, b, c, q : std_logic;
        begin
          reg : process (clk)
            variable acc : integer := 0;
          begin
            if rising_edge(clk) then q <= a; acc := acc + 1; end if;
          end process;
          comb : process (all) begin c <= a and b; end process;
          stim : process begin a <= '1'; wait for 10 ns; wait; end process;
          b <= a or q;
          assert a = '1' or q = '0' report "x";
          ok <= a'stable(5 ns);
          g : for i in 0 to 1 generate
            gp : process (clk) begin if rising_edge(clk) then mem(i) <= v; end if; end process;
          end generate;
        end architecture;
        """;

    private static (ParsedUnit Unit, IrDesign Ir) Lower(string vhdl = Design)
    {
        var unit = EngineFixture.AnalyzeSource(vhdl);
        unit.ThrowIfFailed();
        var top = unit.Entities.Single(e => e.Architecture != null);
        return (unit, unit.IrOf(top));
    }

    private static IrSignal Obj(IEnumerable<IrSignal> objects, string name) =>
        objects.Single(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Ports_keep_their_order_mode_type_and_shape()
    {
        var (_, ir) = Lower();
        Assert.Equal("dut", ir.Name);
        Assert.Equal(["clk", "v", "s", "io", "y", "ok"], ir.Ports.Select(p => p.Name));
        Assert.All(ir.Ports, p => Assert.Equal(IrObjectKind.Port, p.Kind));

        var clk = Obj(ir.Ports, "clk");
        Assert.Equal((IrPortMode.In, IrTypeKind.Logic, 1, (IrRange?)null), (clk.PortMode, clk.Type, clk.Width, clk.Range));
        var v = Obj(ir.Ports, "v");
        Assert.Equal((IrTypeKind.LogicVector, 8, new IrRange(7, 0, true), false), (v.Type, v.Width, v.Range, v.Signed));
        var s = Obj(ir.Ports, "s");
        Assert.Equal((IrTypeKind.LogicVector, 4, true), (s.Type, s.Width, s.Signed));
        Assert.Equal(IrPortMode.InOut, Obj(ir.Ports, "io").PortMode);
        var y = Obj(ir.Ports, "y");
        Assert.Equal((IrPortMode.Out, new IrRange(0, 3, false), false), (y.PortMode, y.Range, y.Signed));
        Assert.Equal(IrTypeKind.Boolean, Obj(ir.Ports, "ok").Type);
    }

    [Fact]
    public void Body_objects_generics_and_variables_are_classified()
    {
        var (_, ir) = Lower();
        var n = Assert.Single(ir.Generics);
        Assert.Equal(("N", IrObjectKind.Generic, (IrPortMode?)null), (n.Name, n.Kind, n.PortMode));

        Assert.Equal(IrObjectKind.Constant, Obj(ir.Signals, "K").Kind);
        var st = Obj(ir.Signals, "st");
        Assert.Equal((IrObjectKind.Signal, IrTypeKind.Enum), (st.Kind, st.Type));
        Assert.Equal(["idle", "run", "done"], st.EnumLiterals!);
        var p = Obj(ir.Signals, "p");
        Assert.Equal(IrTypeKind.Record, p.Type);
        Assert.Equal(3, p.Layout!.Width);
        Assert.Equal(IrTypeKind.Array, Obj(ir.Signals, "mem").Type);
        Assert.Equal(IrTypeKind.Integer, Obj(ir.Signals, "cnt").Type);

        var acc = Assert.Single(ir.Processes.Single(x => x.Label == "reg").Variables);
        Assert.Equal(("acc", IrObjectKind.Variable, IrTypeKind.Integer), (acc.Name, acc.Kind, acc.Type));
    }

    [Fact]
    public void Processes_come_in_declaration_order_with_their_sensitivity()
    {
        var (_, ir) = Lower();
        var units = ir.Processes;
        // reg, comb, stim, b <= …, assert, ok <= …, and one process per generate iteration.
        Assert.Equal(8, units.Count);

        IEnumerable<string> Names(IrProcess x) => x.Sensitivity.Select(s => s.Name.ToLowerInvariant()).Order();

        var reg = units[0];
        Assert.Equal(("reg", IrProcessOrigin.Written, IrSensitivityKind.Explicit), (reg.Label, reg.Origin, reg.SensitivityKind));
        Assert.Equal(["clk"], Names(reg));
        Assert.True(reg.SourceLine > 0);

        var comb = units[1];
        Assert.Equal(IrSensitivityKind.All, comb.SensitivityKind);
        Assert.Equal(["a", "b"], Names(comb));   // c is only written

        var stim = units[2];
        Assert.Equal(IrSensitivityKind.Wait, stim.SensitivityKind);
        Assert.Empty(stim.Sensitivity);

        var assign = units[3];
        Assert.Equal((IrProcessOrigin.Equivalent, IrSensitivityKind.Implicit), (assign.Origin, assign.SensitivityKind));
        Assert.Equal(["a", "q"], Names(assign));   // not the target b

        var assertion = units[4];
        Assert.Equal((IrProcessOrigin.Equivalent, IrSensitivityKind.Implicit), (assertion.Origin, assertion.SensitivityKind));
        Assert.Equal(["a", "q"], Names(assertion));

        // 'stable(T) changes without an event on a: it runs every delta.
        Assert.Equal(IrSensitivityKind.EveryDelta, units[5].SensitivityKind);

        // The generate is unrolled: one written process per iteration, each on clk.
        Assert.All(units.Skip(6), g => Assert.Equal((IrProcessOrigin.Written, IrSensitivityKind.Explicit), (g.Origin, g.SensitivityKind)));
        Assert.All(units.Skip(6), g => Assert.Equal(["clk"], Names(g)));
    }

    [Fact]
    public void Binding_reaches_the_parsed_objects_that_hold_the_values()
    {
        var (unit, ir) = Lower();
        var top = unit.Entities.Single(e => e.Architecture != null);
        foreach (var port in ir.Ports)
            Assert.Same(top.Ports.Single(p => p.Name == port.Name), ir.Binding.Cell(port));
        // What a run registers and traces: the ports and the body objects, in that order.
        Assert.Equal(ir.Objects.Select(o => o.Name), ir.Binding.Objects.Select(o => o.Name));
        Assert.Same(ir, unit.IrOf(top));   // lowered once per unit
    }

    [Fact]
    public void Ports_resolve_whole_or_by_part()
    {
        var (_, ir) = Lower();
        Assert.Equal(new IrPortRef(Obj(ir.Ports, "clk"), null), ir.ResolvePort("CLK"));
        Assert.Equal(new IrPortRef(Obj(ir.Ports, "v"), "v(3)"), ir.ResolvePort("v(3)"));
        Assert.Equal(new IrPortRef(Obj(ir.Ports, "v"), "v(5 downto 2)"), ir.ResolvePort("v(5 downto 2)"));
        Assert.Null(ir.ResolvePort("nope"));
        Assert.Null(ir.ResolvePort("cnt"));   // a body signal is not a port
    }

    [Fact]
    public void A_unit_without_architecture_cannot_be_lowered()
    {
        var unit = EngineFixture.AnalyzeSource("entity lonely is port (a : in bit); end entity;");
        var lonely = Assert.Single(unit.Entities);
        Assert.Throws<InvalidOperationException>(() => VhdlDesignAdapter.From(lonely));
    }
}
