library ieee; use ieee.std_logic_1164.all;
architecture rtl of split_top is
begin
  u: entity work.foo port map (i => n, y => o);
end architecture;
