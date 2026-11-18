-- G31: finish (via use std.env.all) ends the simulation early.
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
use std.env.all;
entity env_finish is
  port (q : out std_logic_vector(3 downto 0));
end entity;
architecture sim of env_finish is
  signal c : unsigned(3 downto 0) := (others => '0');
begin
  process
  begin
    wait for 10 ns;
    c <= c + 1;
    if c = 2 then
      finish;
    end if;
  end process;
  q <= std_logic_vector(c);
end architecture;
