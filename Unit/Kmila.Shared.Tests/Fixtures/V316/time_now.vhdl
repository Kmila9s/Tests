-- G64/G78: the time type, now, wait until ... for, after with a time
-- expression, and time arithmetic/comparison.
library ieee;
use ieee.std_logic_1164.all;

entity time_now is
  port (go : in std_logic; y_loop, y_to, y_hit, y_after, y_cmp : out std_logic);
end entity;

architecture rtl of time_now is
  constant T : time := 4 ns;
  signal d : time := 2 ns;
begin
  -- while now < 30 ns: toggles every 10 ns, then stops
  p_loop: process
  begin
    y_loop <= '0';
    while now < 30 ns loop
      wait for 10 ns;
      y_loop <= not y_loop;
    end loop;
    report "loop done at " & time'image(now);
    wait;
  end process;

  -- wait until go = '1' for 15 ns: times out (go rises at 20 ns)
  p_to: process
  begin
    y_to <= '0';
    wait until go = '1' for 15 ns;
    y_to <= '1';
    report "timeout at " & time'image(now);
    wait;
  end process;

  -- the same wait, but go rises before the timeout (second wait, at 16 ns)
  p_hit: process
  begin
    y_hit <= '0';
    wait for 16 ns;
    wait until go = '1' for 15 ns;
    y_hit <= '1';
    report "hit at " & time'image(now);
    wait;
  end process;

  -- after with a time expression
  y_after <= go after T + d * 2;

  -- time comparison and arithmetic
  y_cmp <= '1' when (T * 3) / 2 = 6 ns and T > d and now >= 0 ns else '0';
end architecture;
