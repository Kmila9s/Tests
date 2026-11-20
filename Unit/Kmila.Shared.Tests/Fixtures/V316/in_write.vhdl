-- G53 side issue: a design must not assign its own input port.
library ieee;
use ieee.std_logic_1164.all;

entity in_write is
  port (a : in std_logic; y : out std_logic);
end entity;

architecture rtl of in_write is
begin
  a <= '1';
  y <= a;
end architecture;
