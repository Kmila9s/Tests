-- v3.1.5 G47/G48: integer mod (sign of the divisor), rem (sign of the
-- dividend), abs and natural division, evaluated while simulating.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity arith_int is
  port (
    sel : in std_logic_vector(1 downto 0);
    y_mod, y_rem, y_div, y_abs : out std_logic_vector(7 downto 0)
  );
end entity;

architecture rtl of arith_int is
  signal a, b : integer := 1;
  signal n, d : natural := 1;
begin
  process (sel)
  begin
    case sel is
      when "00" => a <= -7; b <= 3; n <= 7; d <= 2;
      when "01" => a <= 7; b <= -3; n <= 9; d <= 4;
      when "10" => a <= -7; b <= -3; n <= 1; d <= 3;
      when others => a <= 7; b <= 3; n <= 15; d <= 5;
    end case;
  end process;
  y_mod <= std_logic_vector(to_signed(a mod b, 8));
  y_rem <= std_logic_vector(to_signed(a rem b, 8));
  y_div <= std_logic_vector(to_unsigned(n / d, 8));
  y_abs <= std_logic_vector(to_signed(abs a, 8));
end architecture;
