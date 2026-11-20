library ieee;
use ieee.std_logic_1164.all;

entity tb_time is
end entity;

architecture sim of tb_time is
  signal go : std_logic := '0';
  signal y_loop, y_to, y_hit, y_after, y_cmp : std_logic;
begin
  dut: entity work.time_now port map (go => go, y_loop => y_loop, y_to => y_to, y_hit => y_hit,
    y_after => y_after, y_cmp => y_cmp);
  go <= '1' after 20 ns;
  mon: process
  begin
    wait for 500 ps;
    for t in 0 to 45 loop
      report "t=" & integer'image(t)
        & " tn.y_loop=" & std_logic'image(y_loop)(2) & " tn.y_to=" & std_logic'image(y_to)(2)
        & " tn.y_hit=" & std_logic'image(y_hit)(2) & " tn.y_after=" & std_logic'image(y_after)(2)
        & " tn.y_cmp=" & std_logic'image(y_cmp)(2);
      wait for 1 ns;
    end loop;
    wait;
  end process;
end architecture;
