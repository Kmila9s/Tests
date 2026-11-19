-- G55: VHDL aggregate targets (signal, variable, slices, after, conditional, selected).
library ieee; use ieee.std_logic_1164.all;
entity agg_target is
  port (sel : in std_logic_vector(1 downto 0); v : in std_logic_vector(3 downto 0);
        a, b, c, d, e, f, g, h, p, q, r, s : out std_logic;
        hi, lo : out std_logic_vector(1 downto 0));
end entity;
architecture rtl of agg_target is
begin
  (a, b) <= v(1 downto 0);
  (hi, lo) <= v;
  (c, d) <= std_logic_vector'("10") when sel = "00" else v(3 downto 2);
  with sel select (e, f) <= v(3 downto 2) when "01", std_logic_vector'("11") when others;
  -- The named form `(1 => r, 0 => s) <= …` crashes GHDL 6.0.0
  -- (translate_signal_target_array_aggr: CHOICE_BY_EXPRESSION), so it is not in the repro.
  (r, s) <= v(2 downto 1);
  process (v)
    variable x, y : std_logic;
  begin
    (x, y) := v(2 downto 1);
    (g, h) <= std_logic_vector'(x & y);
    (p, q) <= std_logic_vector'(v(0) & v(3)) after 1 ns;
  end process;
end architecture;
