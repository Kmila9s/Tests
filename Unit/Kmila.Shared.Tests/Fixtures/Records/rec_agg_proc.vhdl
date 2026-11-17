-- G10: named, positional and others aggregates at run time (process), record comparison.
library ieee; use ieee.std_logic_1164.all;
entity rec_agg_proc is
  port (a, b : in std_logic; y : out std_logic_vector(5 downto 0); eq : out std_logic);
end entity;
architecture rtl of rec_agg_proc is
  type pair_t is record
    lo, hi : std_logic;
  end record;
  signal p, q, r : pair_t;
begin
  process (a, b)
  begin
    p <= (lo => a, hi => b);
    q <= (b, a);
    r <= (others => a);
  end process;
  y  <= p.hi & p.lo & q.hi & q.lo & r.hi & r.lo;
  eq <= '1' when p = (lo => '1', hi => '0') else '0';
end architecture;
