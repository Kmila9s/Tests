-- G103: delays below 1 ns and periods that are not a whole number of ns or ps.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity subns is
end entity;
architecture t of subns is
  constant HALF : time := 5.26315789474 ns;   -- 113-edges-det: not a whole ns nor ps
  signal clk : std_logic := '0';
  signal n   : unsigned(3 downto 0) := (others => '0');
  signal q   : std_logic := '0';
  signal d   : std_logic := '0';
begin
  clk <= not clk after HALF;
  q   <= clk after 250 ps;                     -- inertial delay below 1 ns
  process                                      -- 014-mux-sel: wait for 500 ps
  begin
    for i in 0 to 9 loop
      wait for 500 ps;
      n <= n + 1;
    end loop;
    wait for 3*10 ns/4;                        -- 026…029: 7.5 ns (rounded to 8 ns before)
    d <= '1';
    wait;
  end process;
end architecture;
