library ieee;
use ieee.std_logic_1164.all;

entity tb_sens is
end entity;

architecture sim of tb_sens is
  signal a, kin1, kin2 : std_logic_vector(3 downto 0);
  signal c : std_logic;
  signal o1, o2, oc, os, oa, op, pc_out : std_logic_vector(3 downto 0);
begin
  dut: entity work.sens port map (a => a, kin1 => kin1, kin2 => kin2, c => c,
    o1 => o1, o2 => o2, oc => oc, os => os, oa => oa, op => op, pc_out => pc_out);
  stim: process
  begin
    a <= "0101"; kin1 <= "0011"; kin2 <= "0111"; c <= '1';
    wait for 10 ns; kin1 <= "0001"; kin2 <= "0001";   -- only k changes
    wait for 10 ns; a <= "0001";                      -- a changes
    wait for 10 ns; c <= '0';
    wait;
  end process;
  mon: process
  begin
    wait for 5 ns;
    for t in 0 to 3 loop
      report "t=" & integer'image(t * 10 + 5)
        & " se.o1=" & to_string(o1) & " se.o2=" & to_string(o2) & " se.oc=" & to_string(oc)
        & " se.os=" & to_string(os) & " se.oa=" & to_string(oa) & " se.op=" & to_string(op)
        & " se.pc_out=" & to_string(pc_out);
      wait for 10 ns;
    end loop;
    wait;
  end process;
end architecture;
