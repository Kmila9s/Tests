-- G69: & inside a function argument.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity concat_arg is
  port (a : in std_logic_vector(3 downto 0); y, z : out std_logic_vector(7 downto 0));
end entity;

architecture rtl of concat_arg is
begin
  y <= std_logic_vector(resize(signed("0000" & a), 8));
  z <= std_logic_vector(unsigned(a & a) + 1);
end architecture;
