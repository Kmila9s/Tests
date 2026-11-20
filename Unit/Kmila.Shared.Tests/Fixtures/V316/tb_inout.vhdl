-- Testbench for inout_bus: the testbench is a third driver of pad (ext).
library ieee;
use ieee.std_logic_1164.all;

entity tb_inout is
end entity;

architecture sim of tb_inout is
  signal oe1, d1, oe2, d2 : std_logic := '0';
  signal pad : std_logic;
  signal bus4 : std_logic_vector(3 downto 0);
  signal q1, q2, q4 : std_logic;
begin
  dut: entity work.inout_bus port map (oe1 => oe1, d1 => d1, oe2 => oe2, d2 => d2, pad => pad,
    q1 => q1, q2 => q2, bus4 => bus4, q4 => q4);
  stim: process
  begin
    pad <= 'Z'; bus4 <= "ZZZZ";
    wait for 10 ns; oe1 <= '1'; d1 <= '1';             -- c1 drives 1
    wait for 10 ns; oe2 <= '1'; d2 <= '0';             -- c1=1, c2=0 -> X
    wait for 10 ns; oe1 <= '0'; oe2 <= '0';            -- nobody drives -> Z
    wait for 10 ns; pad <= '1'; bus4 <= "H0Z1";        -- testbench drives
    wait for 10 ns; oe1 <= '1'; d1 <= '0'; d2 <= '1';  -- tb 1 vs c1 0 -> X; c3 drives bus4(2)
    wait;
  end process;
  mon: process
  begin
    wait for 5 ns;
    for k in 0 to 5 loop
      report "t=" & integer'image(k * 10 + 5)
        & " io.pad=" & std_logic'image(pad)(2) & " io.q1=" & std_logic'image(q1)(2)
        & " io.q2=" & std_logic'image(q2)(2) & " io.bus4=" & to_string(bus4)
        & " io.q4=" & std_logic'image(q4)(2);
      wait for 10 ns;
    end loop;
    wait;
  end process;
end architecture;
