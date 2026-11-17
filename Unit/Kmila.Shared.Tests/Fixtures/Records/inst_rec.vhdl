-- G14/G15: two instances of an entity with a local record type and record ports;
-- partial (per-field) association in u1, whole-record association in u2.
library ieee; use ieee.std_logic_1164.all;
package inst_rec_types is
  type pair_t is record
    lo, hi : std_logic;
  end record;
end package;

library ieee; use ieee.std_logic_1164.all;
use work.inst_rec_types.all;
entity cell is
  port (p : in pair_t; q : out pair_t);
end entity;
architecture rtl of cell is
  type st_t is record
    v, n : std_logic;
  end record;
  signal s : st_t;
begin
  s.v  <= p.lo xor p.hi;
  s.n  <= not p.lo;
  q.lo <= s.v;
  q.hi <= s.n;
end architecture;

library ieee; use ieee.std_logic_1164.all;
use work.inst_rec_types.all;
entity inst_rec is
  port (a, b : in std_logic; y1, z1, y2, z2 : out std_logic);
end entity;
architecture rtl of inst_rec is
  signal pp, qq : pair_t;
begin
  pp <= (lo => b, hi => a);
  u1 : entity work.cell port map (p.lo => a, p.hi => b, q.lo => y1, q.hi => z1);
  u2 : entity work.cell port map (p => pp, q => qq);
  y2 <= qq.lo;
  z2 <= qq.hi;
end architecture;
