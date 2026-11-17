-- G09: nested record, multi-level field access (q.a.lo) and whole-field copy (q.b <= q.a).
library ieee; use ieee.std_logic_1164.all;
entity rec_nested is
  port (x, w : in std_logic; o : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of rec_nested is
  type pair_t is record
    lo, hi : std_logic;
  end record;
  type quad_t is record
    a : pair_t;
    b : pair_t;
  end record;
  signal q : quad_t;
begin
  q.a.lo <= x;
  q.a.hi <= w;
  q.b.lo <= q.a.hi;
  q.b.hi <= q.a.lo;
  o <= q.b.hi & q.b.lo & q.a.hi & q.a.lo;
end architecture;
