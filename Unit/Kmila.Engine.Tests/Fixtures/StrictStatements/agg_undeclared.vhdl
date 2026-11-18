library ieee; use ieee.std_logic_1164.all;
entity agg_undeclared is
  port (v : in std_logic_vector(1 downto 0); a : out std_logic);
end entity;
architecture rtl of agg_undeclared is
begin
  (a, zz) <= v;
end architecture;
