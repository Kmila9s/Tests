-- v3.1.5 G49: writing a vector element with a run-time index outside its
-- range is a run-time error.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity idx_wr is
  port (
    i : in std_logic_vector(2 downto 0);
    y : out std_logic_vector(3 downto 0)
  );
end entity;

architecture rtl of idx_wr is
begin
  process (i)
    variable v : std_logic_vector(3 downto 0);
  begin
    v := "0000";
    v(to_integer(unsigned(i))) := '1';
    y <= v;
  end process;
end architecture;
