library ieee; use ieee.std_logic_1164.all;
entity aggpos is port (a, b : in std_logic; y, z : out std_logic); end entity;
architecture rtl of aggpos is
  type pair_t is record
    lo : std_logic;
    hi : std_logic;
  end record;
  signal p, q : pair_t;
begin
  p <= (a, b); q <= p; y <= q.lo; z <= q.hi;
end architecture;
