library ieee; use ieee.std_logic_1164.all;
entity agg_named is
  port (v : in std_logic_vector(1 downto 0); a, b : out std_logic);
end entity;
architecture rtl of agg_named is
begin
  (1 => a, 0 => b) <= v;
end architecture;
