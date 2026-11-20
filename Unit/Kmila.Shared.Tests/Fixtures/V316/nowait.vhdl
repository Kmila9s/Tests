-- G72: a process with neither a sensitivity list nor a wait statement.
library ieee;
use ieee.std_logic_1164.all;

entity nowait is
  port (a : in std_logic; y : out std_logic);
end entity;

architecture rtl of nowait is
begin
  process
  begin
    y <= a;
  end process;
end architecture;
