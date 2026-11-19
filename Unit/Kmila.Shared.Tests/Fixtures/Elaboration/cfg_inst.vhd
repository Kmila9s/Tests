-- G20: `u : configuration work.cfg` instance.
library ieee; use ieee.std_logic_1164.all;
entity foo is port (y : out std_logic_vector(3 downto 0)); end entity;
architecture arch_a of foo is begin y <= "1010"; end architecture;
architecture arch_b of foo is begin y <= "0101"; end architecture;

configuration cfg_a of foo is
  for arch_a end for;
end configuration;

library ieee; use ieee.std_logic_1164.all;
entity cfg_inst is port (oa, ob : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of cfg_inst is
begin
  u1: configuration work.cfg_a port map (y => oa);
  u2: entity work.foo port map (y => ob);
end architecture;
