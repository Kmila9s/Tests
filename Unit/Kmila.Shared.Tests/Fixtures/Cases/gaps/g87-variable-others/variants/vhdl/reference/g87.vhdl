library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity g87 is
end entity;
architecture t of g87 is
  signal g : std_logic_vector(3 downto 0) := (others => '0');
begin
  process
    variable idx    : unsigned(1 downto 0) := "01";
    variable next_g : std_logic_vector(3 downto 0);
  begin
    next_g := (others => '0');
    report "a=" & to_string(next_g);
    next_g(to_integer(idx)) := '1';
    report "b=" & to_string(next_g);
    g <= next_g;
    wait for 1 ns;
    report "g=" & to_string(g);
    wait;
  end process;
end architecture;
