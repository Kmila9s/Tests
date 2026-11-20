-- G59: signed arithmetic and conversions (numeric_std).
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity numeric_signed is
  port (a, b : in std_logic_vector(3 downto 0);
        ti : out integer range -8 to 7;
        rz8 : out std_logic_vector(7 downto 0);   -- resize grows (sign extension)
        rz2 : out std_logic_vector(1 downto 0);   -- resize shrinks (keeps the sign)
        sr, sl, ab, ng : out std_logic_vector(3 downto 0);
        lt, gt0, eqw : out std_logic;
        mi : out std_logic_vector(7 downto 0);   -- signed * integer: 2L bits
        dv, md, rm : out std_logic_vector(3 downto 0));
end entity;

architecture rtl of numeric_signed is
  signal sa, sb : signed(3 downto 0);
begin
  sa <= signed(a);
  sb <= signed(b);
  ti  <= to_integer(sa);
  rz8 <= std_logic_vector(resize(sa, 8));
  rz2 <= std_logic_vector(resize(sa, 2));
  sr  <= std_logic_vector(shift_right(sa, 1));
  sl  <= std_logic_vector(shift_left(sa, 1));
  ab  <= std_logic_vector(abs sa);
  ng  <= std_logic_vector(-sa);
  lt  <= '1' when sa < sb else '0';
  gt0 <= '1' when sa > 0 else '0';
  eqw <= '1' when sa = resize(sb, 6) else '0';   -- different lengths
  mi  <= std_logic_vector(sa * 3);
  dv  <= std_logic_vector(sa / sb) when sb /= 0 else "0000";
  md  <= std_logic_vector(sa mod sb) when sb /= 0 else "0000";
  rm  <= std_logic_vector(sa rem sb) when sb /= 0 else "0000";
end architecture;
