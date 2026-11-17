-- G01/G11: record-typed process variable, field := and whole-record aggregate :=.
library ieee; use ieee.std_logic_1164.all;
entity rec_var is
  port (a, b : in std_logic; y, z, u : out std_logic);
end entity;
architecture rtl of rec_var is
  type pair_t is record
    lo, hi : std_logic;
  end record;
begin
  process (a, b)
    variable v : pair_t;
  begin
    v.lo := a;
    v.hi := b;
    u <= v.lo and v.hi;
    v := (lo => v.hi, hi => v.lo);
    y <= v.lo;
    z <= v.hi;
  end process;
end architecture;
