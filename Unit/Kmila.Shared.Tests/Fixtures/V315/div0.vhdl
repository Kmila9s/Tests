-- v3.1.5 G46: integer division by zero while simulating is a run-time error.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity div0 is
  port (
    z : in std_logic_vector(3 downto 0);
    y : out std_logic_vector(3 downto 0)
  );
end entity;

architecture rtl of div0 is
begin
  process (z)
    variable q : integer;
  begin
    q := 12 / to_integer(unsigned(z));
    y <= std_logic_vector(to_unsigned(q, 4));
  end process;
end architecture;
