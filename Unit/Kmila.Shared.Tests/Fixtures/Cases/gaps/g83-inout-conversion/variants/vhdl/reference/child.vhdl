library ieee; use ieee.std_logic_1164.all;
entity child is port (en : in std_logic; b : inout std_logic_vector(3 downto 0)); end entity;
architecture rtl of child is begin
  b <= "1010" when en = '1' else (others => 'Z');
end architecture;
