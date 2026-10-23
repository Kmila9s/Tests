library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity adder8 is
  port (
    a : in  std_logic_vector(7 downto 0);
    b : in  std_logic_vector(7 downto 0);
    s : out std_logic_vector(7 downto 0)
  );
end entity adder8;

architecture rtl of adder8 is
begin
  s <= std_logic_vector(unsigned(a) + unsigned(b));
end architecture rtl;
