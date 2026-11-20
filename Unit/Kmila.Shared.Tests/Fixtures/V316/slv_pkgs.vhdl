-- G59: std_logic_vector arithmetic follows the package in the use clause.
library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_signed.all;

entity slv_signed is
  port (a, b : in std_logic_vector(3 downto 0); lt : out std_logic; s : out std_logic_vector(3 downto 0);
        i : out integer);
end entity;

architecture rtl of slv_signed is
begin
  lt <= '1' when a < b else '0';
  s <= a + b;
  i <= conv_integer(a);
end architecture;

library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_unsigned.all;

entity slv_unsigned is
  port (a, b : in std_logic_vector(3 downto 0); lt : out std_logic; s : out std_logic_vector(3 downto 0);
        i : out integer);
end entity;

architecture rtl of slv_unsigned is
begin
  lt <= '1' when a < b else '0';
  s <= a + b;
  i <= conv_integer(a);
end architecture;

library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_arith.all;

entity slv_arith is
  port (a : in std_logic_vector(3 downto 0); sx, ex : out std_logic_vector(7 downto 0));
end entity;

architecture rtl of slv_arith is
begin
  sx <= sxt(a, 8);
  ex <= ext(a, 8);
end architecture;
