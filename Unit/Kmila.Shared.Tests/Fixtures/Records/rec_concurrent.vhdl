library ieee;
use ieee.std_logic_1164.all;
entity rec is
  port (a : in std_logic; b : in std_logic; y : out std_logic; z : out std_logic);
end entity rec;
architecture rtl of rec is
  type pair_t is record
    lo : std_logic;
    hi : std_logic;
  end record;
  signal p : pair_t;
begin
  p.lo <= a;
  p.hi <= b;
  y <= p.lo;
  z <= p.hi;
end architecture rtl;
