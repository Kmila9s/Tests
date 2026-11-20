library ieee;
use ieee.std_logic_1164.all;

entity tb_mixed is
end entity;

architecture sim of tb_mixed is
  signal clk, d, q : std_logic := '0';
  signal n : std_logic_vector(1 downto 0);
begin
  dut: entity work.mixed_case port map (clk => clk, d => d, q => q, n => n);
  clk <= not clk after 5 ns;
  d <= '1' after 7 ns, '0' after 27 ns;
  mon: process
  begin
    wait for 2 ns;
    for k in 0 to 4 loop
      report "t=" & integer'image(k * 10 + 2) & " mc.q=" & std_logic'image(q)(2) & " mc.n=" & to_string(n);
      wait for 10 ns;
    end loop;
    wait;
  end process;
end architecture;
