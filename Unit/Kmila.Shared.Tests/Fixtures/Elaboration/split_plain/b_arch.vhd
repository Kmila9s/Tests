library ieee; use ieee.std_logic_1164.all;
architecture rtl of split_plain is begin o <= n(0) & n(3 downto 1); end architecture;
