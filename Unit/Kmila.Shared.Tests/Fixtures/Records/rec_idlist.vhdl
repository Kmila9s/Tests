library ieee; use ieee.std_logic_1164.all;
entity idlist is port (a, b : in std_logic; y, z : out std_logic); end entity;
architecture rtl of idlist is
  type pair_t is record
    lo, hi : std_logic;
  end record;
  signal p, q : pair_t;
begin
  p.lo <= a; p.hi <= b; y <= p.lo; z <= p.hi;
end architecture;
