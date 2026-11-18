library ieee; use ieee.std_logic_1164.all;
entity top is port (o : out std_logic_vector(3 downto 0)); end entity;
architecture first_a of top is begin o <= "1010"; end architecture;
architecture last_b of top is begin o <= "0101"; end architecture;
