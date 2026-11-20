-- v3.1.5 G41: IEEE 1164 nine-valued operators, VHDL-2008 reduction and
-- matching operators, ?? (explicit and implicit) and the predicate functions.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity logic9 is
  port (
    a, b : in std_logic;
    v : in std_logic_vector(3 downto 0);
    y_and, y_or, y_xor, y_nand, y_nor, y_xnor, y_not : out std_logic;
    r_and, r_or, r_xor : out std_logic;
    m_eq, m_ne : out std_logic;
    q1, q2, eqx, eqa, sm, isx : out std_logic;
    x01 : out std_logic;
    v01 : out std_logic_vector(3 downto 0);
    vand : out std_logic_vector(3 downto 0)
  );
end entity;

architecture rtl of logic9 is
begin
  y_and <= a and b;
  y_or <= a or b;
  y_xor <= a xor b;
  y_nand <= a nand b;
  y_nor <= a nor b;
  y_xnor <= a xnor b;
  y_not <= not a;
  r_and <= and v;
  r_or <= or v;
  r_xor <= xor v;
  m_eq <= a ?= b;
  m_ne <= a ?/= b;
  q1 <= '1' when ?? a else '0';
  q2 <= '1' when a else '0';
  eqx <= '1' when a = 'X' else '0';
  eqa <= '1' when a = b else '0';
  sm <= '1' when std_match(a, b) else '0';
  isx <= '1' when is_x(v) else '0';
  x01 <= to_x01(a);
  v01 <= std_logic_vector(to_01(unsigned(v)));
  vand <= v and (a & b & a & b);
end architecture;
