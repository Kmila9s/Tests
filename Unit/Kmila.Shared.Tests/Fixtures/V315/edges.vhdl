-- v3.1.5 G41: rising_edge/falling_edge use To_X01 of the previous and the
-- current value (U->1 and X->1 are not rising edges; 0->H is), 'event counts
-- every change.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity edges is
  port (
    c : in std_logic;
    n_rise, n_fall, n_ev : out std_logic_vector(3 downto 0)
  );
end entity;

architecture rtl of edges is
  signal r, f, e : unsigned(3 downto 0) := "0000";
begin
  process (c)
  begin
    if rising_edge(c) then
      r <= r + 1;
    end if;
    if falling_edge(c) then
      f <= f + 1;
    end if;
    if c'event then
      e <= e + 1;
    end if;
  end process;
  n_rise <= std_logic_vector(r);
  n_fall <= std_logic_vector(f);
  n_ev <= std_logic_vector(e);
end architecture;
