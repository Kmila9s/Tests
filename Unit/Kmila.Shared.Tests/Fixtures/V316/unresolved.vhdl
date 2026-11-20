-- G71: two drivers on an unresolved signal is an elaboration error.
library ieee;
use ieee.std_logic_1164.all;

entity unres_sul is
  port (a, b : in std_ulogic; y : out std_ulogic);
end entity;

architecture rtl of unres_sul is
begin
  y <= a;
  y <= b;
end architecture;

entity unres_int is
  port (a, b : in integer; y : out integer);
end entity;

architecture rtl of unres_int is
begin
  p1: process (a) begin y <= a; end process;
  p2: process (b) begin y <= b; end process;
end architecture;

entity unres_bit is
  port (a, b : in bit; y : out bit);
end entity;

architecture rtl of unres_bit is
  signal s : bit;
begin
  s <= a;
  s <= b;
  y <= s;
end architecture;
