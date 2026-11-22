library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;
entity tb is
end entity;
architecture t of tb is
  signal abcd : std_logic_vector(3 downto 0) := "0000";
  signal f : std_logic;
begin
  dut: entity work.problem_5_2_3 port map (abcd => abcd, f => f);
  process
  begin
    for i in 0 to 15 loop
      abcd <= std_logic_vector(to_unsigned(i, 4));
      wait for 1 ns;
      report integer'image(i) & " f=" & std_logic'image(f);
    end loop;
    wait;
  end process;
end architecture;
