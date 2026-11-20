-- Testbench for drivers and after_w: samples every ns at k+0.5 ns.
library ieee;
use ieee.std_logic_1164.all;

entity tb_v316b is
end entity;

architecture sim of tb_v316b is
  signal a, b, ea, eb : std_logic := '0';
  signal y2, bus_t, lh : std_logic;
  signal v, w : std_logic_vector(3 downto 0);
  signal k : std_logic_vector(1 downto 0);
  signal y_wave, y_inert, y_trans, y_rej, y_seq, r : std_logic;
begin
  ud: entity work.drivers port map (a => a, b => b, ea => ea, eb => eb, y2 => y2, bus_t => bus_t,
    lh => lh, v => v, w => w, k => k);
  ua: entity work.after_w port map (a => a, y_wave => y_wave, y_inert => y_inert,
    y_trans => y_trans, y_rej => y_rej, y_seq => y_seq, r => r);
  stim: process
  begin
    a <= '0'; b <= '0'; ea <= '0'; eb <= '0';
    wait for 10 ns; a <= '1';               -- 10: a=1 b=0
    wait for 2 ns;  a <= '0';               -- 12: 2 ns pulse
    wait for 8 ns;  a <= '1'; b <= '1';     -- 20: a=b=1
    wait for 6 ns;  a <= '0'; ea <= '1';    -- 26: 6 ns pulse ends; ea=1
    wait for 4 ns;  a <= '1';               -- 30
    wait for 1 ns;  a <= '0'; eb <= '1';    -- 31: 1 ns pulse; ea=eb=1, a=0, b=1
    wait for 9 ns;  ea <= '0'; b <= 'Z';    -- 40
    wait for 5 ns;  a <= 'H'; b <= 'L';     -- 45
    wait;
  end process;
  mon: process
  begin
    wait for 500 ps;
    for t in 0 to 49 loop
      report "t=" & integer'image(t)
        & " dr.y2=" & std_logic'image(y2)(2) & " dr.bus_t=" & std_logic'image(bus_t)(2)
        & " dr.lh=" & std_logic'image(lh)(2) & " dr.v=" & to_string(v)
        & " dr.w=" & to_string(w) & " dr.k=" & to_string(k)
        & " aw.y_wave=" & std_logic'image(y_wave)(2) & " aw.y_inert=" & std_logic'image(y_inert)(2)
        & " aw.y_trans=" & std_logic'image(y_trans)(2) & " aw.y_rej=" & std_logic'image(y_rej)(2)
        & " aw.y_seq=" & std_logic'image(y_seq)(2) & " aw.r=" & std_logic'image(r)(2);
      wait for 1 ns;
    end loop;
    wait;
  end process;
end architecture;
