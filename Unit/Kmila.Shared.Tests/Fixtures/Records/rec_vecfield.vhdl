library ieee; use ieee.std_logic_1164.all;
entity vecfield is port (a, b : in std_logic; y, z : out std_logic); end entity;
architecture rtl of vecfield is
  type pair_t is record
    lo : std_logic;
    d : std_logic_vector(3 downto 0);
  end record;
  signal p, q : pair_t;
begin
  p.d <= a & b & a & b; p.lo <= a; y <= p.d(2); z <= p.lo;
end architecture;
