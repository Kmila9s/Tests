library ieee; use ieee.std_logic_1164.all;
entity order_top is port (o : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of order_top is
begin
  u: entity work.foo port map (y => o);
end architecture;
