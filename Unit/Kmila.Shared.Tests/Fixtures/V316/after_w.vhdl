-- G63: waveforms (several elements), inertial / transport / reject delay,
-- and a matured transaction seen by a process that wakes at the same time.
library ieee;
use ieee.std_logic_1164.all;

entity after_w is
  port (a : in std_logic;
        y_wave, y_inert, y_trans, y_rej, y_seq, r : out std_logic);
end entity;

architecture rtl of after_w is
  signal s : std_logic := '0';
begin
  y_wave  <= '1', '0' after 5 ns, '1' after 10 ns, 'Z' after 12 ns;
  y_inert <= a after 4 ns;
  y_trans <= transport a after 4 ns;
  y_rej   <= reject 1 ns inertial a after 4 ns;

  -- Inertial preemption inside one driver: the second assignment of the same
  -- process run deletes the pending transaction of the first.
  p_seq: process (a)
  begin
    y_seq <= '1' after 3 ns;
    y_seq <= a after 6 ns;
  end process;

  -- s matures at 10 ns; the process waking at 10 ns must already see it.
  p_s: process begin s <= '1' after 10 ns; wait; end process;
  p_r: process begin wait for 10 ns; r <= s; wait; end process;
end architecture;
