library ieee; use ieee.std_logic_1164.all;
entity foo is port (y : out std_logic_vector(3 downto 0)); end entity;
architecture arch_a of foo is begin y <= "1010"; end architecture;
library ieee; use ieee.std_logic_1164.all;
entity top is port (o : out std_logic_vector(3 downto 0)); end entity;
architecture first_a of top is begin u1: entity work.foo port map (y => o); end architecture;
architecture last_b of top is begin o <= "0110"; end architecture;
