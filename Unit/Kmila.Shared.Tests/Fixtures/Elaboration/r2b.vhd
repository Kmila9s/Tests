library ieee; use ieee.std_logic_1164.all;
entity foo is port (y : out std_logic_vector(3 downto 0)); end entity;
architecture arch_a of foo is begin y <= "1010"; end architecture;
architecture arch_b of foo is begin y <= "0101"; end architecture;

library ieee; use ieee.std_logic_1164.all;
entity top is port (oa : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of top is
  component foo is port (y : out std_logic_vector(3 downto 0)); end component;
  for u1 : foo use entity work.foo(arch_b);
begin
  u1: foo port map (y => oa);
end architecture;
