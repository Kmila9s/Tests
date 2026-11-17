using Kmila.Shared.Models;
using Kmila.Shared.Services.Api;

namespace Kmila.Shared.Tests.Engine;

/// <summary>
/// v3.1.1 (G02–G08, G50, G51): every assignment form writes only the bit,
/// range or element its target names — concurrent, process, selected and
/// conditional, with <c>after</c>, on variables (<c>:=</c>) and at run-time
/// indices. Expected values come from GHDL 6.0.0
/// (<c>Documentacion/handoffs/repros_gaps_2026-11-16/v311/tb_v311.vhdl</c>);
/// the designs below are those repros verbatim.
/// </summary>
public class PartialAssignmentTests
{
    private const string SelBit = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity sel_bit is
          port (s, a, b : in std_logic;
                y : out std_logic_vector(1 downto 0);
                z : out std_logic_vector(3 downto 0));
        end entity sel_bit;
        architecture rtl of sel_bit is
        begin
          with s select y(0) <= a when '1', '0' when others;
          with s select y(1) <= b when '1', '1' when others;
          z(3 downto 2) <= a & b when s = '1' else "11";
          z(1 downto 0) <= b & a when s = '1' else "00";
        end architecture rtl;
        """;

    private const string AfterSlice = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity after_slice is
          port (a, b : in std_logic; y : out std_logic_vector(3 downto 0));
        end entity after_slice;
        architecture rtl of after_slice is
          signal v : std_logic_vector(3 downto 0) := "0000";
        begin
          v(3 downto 2) <= a & b after 5 ns;
          v(1 downto 0) <= "11";
          y <= v;
        end architecture rtl;
        """;

    private const string VarBits = """
        library ieee;
        use ieee.std_logic_1164.all;
        entity var_bits is
          port (a, b : in std_logic; y, w : out std_logic_vector(1 downto 0));
        end entity var_bits;
        architecture rtl of var_bits is
          signal v : std_logic_vector(1 downto 0);
        begin
          process (a, b)
            variable t : std_logic_vector(1 downto 0);
          begin
            t := "00";
            t(1) := a;
            t(0) := b;
            y <= t;
          end process;
          process (a)
          begin
            v <= "00";
            v(1) <= a;
          end process;
          w <= v;
        end architecture rtl;
        """;

    private const string RomRange = """
        library ieee;
        use ieee.std_logic_1164.all;
        use ieee.numeric_std.all;
        entity rom_range is
          port (sel : in std_logic_vector(1 downto 0); y, z : out std_logic_vector(3 downto 0));
        end entity rom_range;
        architecture rtl of rom_range is
          type up_t is array (1 to 4) of std_logic_vector(3 downto 0);
          type dn_t is array (3 downto 0) of std_logic_vector(3 downto 0);
          constant up : up_t := (1 => "0001", 2 => "0010", 3 => "0100", 4 => "1000");
          constant dn : dn_t := ("0011", "0101", "0110", "1001");
        begin
          y <= up(to_integer(unsigned(sel)) + 1);
          z <= dn(to_integer(unsigned(sel)));
        end architecture rtl;
        """;

    private const string Ram = """
        library ieee;
        use ieee.std_logic_1164.all;
        use ieee.numeric_std.all;
        entity ram is
          port (clk : in std_logic; we : in std_logic; addr : in std_logic_vector(1 downto 0);
                din : in std_logic_vector(3 downto 0); dout : out std_logic_vector(3 downto 0));
        end entity ram;
        architecture rtl of ram is
          type mem_t is array (0 to 3) of std_logic_vector(3 downto 0);
          signal mem : mem_t := (others => (others => '0'));
        begin
          process (clk)
          begin
            if rising_edge(clk) then
              if we = '1' then
                mem(to_integer(unsigned(addr))) <= din;
              end if;
            end if;
          end process;
          dout <= mem(to_integer(unsigned(addr)));
        end architecture rtl;
        """;

    private static PortMomentValue At(int ns, string v) => new() { Time = ns, TimeScale = "NS", Value = v };

    private static PortMomentConfiguration Port(string name, params PortMomentValue[] values) =>
        new() { PortName = name, Values = [.. values] };

    /// <summary>Simulates <paramref name="vhdl"/> for 100 ns at 1 ns resolution (50 MHz clock) and returns its waveform.</summary>
    private static async Task<WaveformData> SimulateAsync(string vhdl, string entry, params PortMomentConfiguration[] moments)
    {
        var store = new SandboxProjectStore();
        var project = store.Create("partial-assign", retain: false);
        try
        {
            project.WriteFile($"{entry}.vhdl", vhdl);
            var run = await HeadlessSimulator.RunProjectAsync(project.Path, new HeadlessRunOptions
            {
                EntryPoint = entry,
                TotalNs = 100,
                TickResNs = 1,
                SystemClockMhz = 50,
                PortMoments = [.. moments],
            });
            Assert.Null(run.EngineError);
            return WaveformData.FromSignalHistory(run.History, 1);
        }
        finally { store.Delete(project.Id); }
    }

    private static double? ValueAt(WaveformData wf, string signal, long ns) =>
        wf.Signals.Single(s => s.Name.Equals(signal, StringComparison.OrdinalIgnoreCase)).ValueAtNs(ns).Value;

    [Fact]
    public async Task Selected_and_conditional_assignments_write_only_their_bit_or_range()
    {
        var wf = await SimulateAsync(SelBit, "sel_bit",
            Port("s", At(0, "1"), At(20, "0")), Port("a", At(0, "1"), At(20, "0")), Port("b", At(0, "0"), At(20, "1")));
        Assert.Equal(0b01, ValueAt(wf, "y", 7));
        Assert.Equal(0b1001, ValueAt(wf, "z", 7));
        Assert.Equal(0b10, ValueAt(wf, "y", 27));
        Assert.Equal(0b1100, ValueAt(wf, "z", 27));
    }

    [Fact]
    public async Task A_delayed_slice_write_lands_after_its_delay_and_keeps_the_other_bits()
    {
        var wf = await SimulateAsync(AfterSlice, "after_slice", Port("a", At(0, "1")), Port("b", At(0, "0")));
        Assert.Equal(0b0011, ValueAt(wf, "y", 3));
        Assert.Equal(0b1011, ValueAt(wf, "y", 7));
    }

    [Fact]
    public async Task Variable_bits_are_immediate_and_a_later_bit_write_overrides_a_whole_write()
    {
        var wf = await SimulateAsync(VarBits, "var_bits",
            Port("a", At(0, "1"), At(20, "0")), Port("b", At(0, "0"), At(20, "1")));
        Assert.Equal(0b10, ValueAt(wf, "y", 7));
        Assert.Equal(0b10, ValueAt(wf, "w", 7));
        Assert.Equal(0b01, ValueAt(wf, "y", 27));
        Assert.Equal(0b00, ValueAt(wf, "w", 27));
    }

    [Fact]
    public async Task Arrays_index_by_their_declared_bounds_and_direction()
    {
        var wf = await SimulateAsync(RomRange, "rom_range", Port("sel", At(0, "00"), At(20, "10"), At(40, "11")));
        Assert.Equal(0b0001, ValueAt(wf, "y", 7));    // up(1)
        Assert.Equal(0b1001, ValueAt(wf, "z", 7));    // dn(0): rightmost of a downto array
        Assert.Equal(0b0100, ValueAt(wf, "y", 27));   // up(3)
        Assert.Equal(0b0101, ValueAt(wf, "z", 27));   // dn(2)
        Assert.Equal(0b1000, ValueAt(wf, "y", 55));   // up(4)
        Assert.Equal(0b0011, ValueAt(wf, "z", 55));   // dn(3): leftmost
    }

    [Fact]
    public async Task A_ram_writes_at_a_run_time_index_and_starts_from_its_nested_aggregate()
    {
        var wf = await SimulateAsync(Ram, "ram",
            Port("we", At(0, "1"), At(40, "0")),
            Port("addr", At(0, "01"), At(20, "10"), At(50, "01"), At(70, "10"), At(90, "00")),
            Port("din", At(0, "0101"), At(20, "1001")));
        Assert.Equal(0b0101, ValueAt(wf, "dout", 55));
        Assert.Equal(0b1001, ValueAt(wf, "dout", 75));
        Assert.Equal(0b0000, ValueAt(wf, "dout", 95));
    }
}
