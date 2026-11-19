-- G19: `for all : c use entity …(arch);` binds every instance of c.
library ieee; use ieee.std_logic_1164.all;
entity foo is port (y : out std_logic_vector(3 downto 0)); end entity;
architecture arch_a of foo is begin y <= "1010"; end architecture;
architecture arch_b of foo is begin y <= "0101"; end architecture;

library ieee; use ieee.std_logic_1164.all;
entity cfg_spec_all is port (oa, ob : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of cfg_spec_all is
  component foo is port (y : out std_logic_vector(3 downto 0)); end component;
  for all : foo use entity work.foo(arch_a);
begin
  u1: foo port map (y => oa);
  u2: foo port map (y => ob);
end architecture;
