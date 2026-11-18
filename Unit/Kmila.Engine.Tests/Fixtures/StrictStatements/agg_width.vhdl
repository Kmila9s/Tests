library ieee; use ieee.std_logic_1164.all;
entity agg_width is
  port (v : in std_logic_vector(2 downto 0); a, b : out std_logic);
end entity;
architecture rtl of agg_width is
begin
  (a, b) <= v;
end architecture;
