library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity waittick is
end entity;
architecture t of waittick is
  signal n : unsigned(4 downto 0) := (others => '0');
  signal q : std_logic := '0';
begin
  q <= not q after 3 ns;
  process
  begin
    for i in 0 to 15 loop
      wait for 1 ns;
      n <= n + 1;
    end loop;
    wait;
  end process;
end architecture;
