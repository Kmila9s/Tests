library ieee;
use ieee.std_logic_1164.all;
entity g85 is
end entity;
architecture t of g85 is
  signal i : std_logic_vector(3 downto 0) := "1000";
  signal q : std_logic_vector(3 downto 0);
begin
  process (i)
  begin
    q <= (others => i(3));
  end process;
  process
  begin
    wait for 1 ns;
    report "q=" & to_string(q);
    i <= "0111";
    wait for 1 ns;
    report "q=" & to_string(q);
    wait;
  end process;
end architecture;
