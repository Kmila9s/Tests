-- G31: severity failure ends the simulation with an error.
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity env_fail is
  port (q : out std_logic_vector(3 downto 0));
end entity;
architecture sim of env_fail is
  signal c : unsigned(3 downto 0) := (others => '0');
begin
  process
  begin
    wait for 10 ns;
    c <= c + 1;
    assert c /= 3 report "env_fail: c reached 3" severity failure;
  end process;
  q <= std_logic_vector(c);
end architecture;
