library ieee; use ieee.std_logic_1164.all;
entity nested is port (a, b : in std_logic; y, z : out std_logic); end entity;
architecture rtl of nested is
  type pair_t is record
    lo : std_logic;
    hi : std_logic;
  end record;
  signal p, q : pair_t;
begin
  q.lo <= p.hi; p.hi <= b; p.lo <= a; y <= q.lo; z <= p.lo;
end architecture;
