-- G09: vector field bounded by a constant; field slice and bit reads (r.d(2 downto 1), r.d(0)).
library ieee; use ieee.std_logic_1164.all;
entity rec_vecconst is
  port (a : in std_logic; din : in std_logic_vector(3 downto 0); y : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of rec_vecconst is
  constant W : integer := 4;
  type bus_t is record
    d : std_logic_vector(W-1 downto 0);
    v : std_logic;
  end record;
  signal r : bus_t;
begin
  r.d <= din;
  r.v <= a;
  y <= r.d(2 downto 1) & r.v & r.d(0);
end architecture;
