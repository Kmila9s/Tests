-- v3.1.5 G49: reading a vector with a run-time index outside its range is a
-- run-time error.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity idx_rd is
  port (
    i : in std_logic_vector(2 downto 0);
    y : out std_logic
  );
end entity;

architecture rtl of idx_rd is
  signal v : std_logic_vector(3 downto 0) := "1010";
begin
  process (i)
  begin
    y <= v(to_integer(unsigned(i)));
  end process;
end architecture;
