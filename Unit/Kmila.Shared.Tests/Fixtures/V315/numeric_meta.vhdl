-- v3.1.5 G41: numeric_std arithmetic, relations and conversions with
-- metavalues (result X / FALSE / 0 plus a warning) and H/L as 1/0.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity numeric_meta is
  port (
    x, y : in std_logic_vector(3 downto 0);
    s_add, s_sub : out std_logic_vector(3 downto 0);
    s_mul : out std_logic_vector(7 downto 0);
    lt, eq : out std_logic;
    ti : out std_logic_vector(3 downto 0);
    rs : out std_logic_vector(5 downto 0);
    sr : out std_logic_vector(3 downto 0)
  );
end entity;

architecture rtl of numeric_meta is
begin
  s_add <= std_logic_vector(unsigned(x) + unsigned(y));
  s_sub <= std_logic_vector(unsigned(x) - unsigned(y));
  s_mul <= std_logic_vector(unsigned(x) * unsigned(y));
  lt <= '1' when unsigned(x) < unsigned(y) else '0';
  eq <= '1' when unsigned(x) = unsigned(y) else '0';
  ti <= std_logic_vector(to_unsigned(to_integer(unsigned(x)), 4));
  rs <= std_logic_vector(resize(unsigned(x), 6));
  sr <= std_logic_vector(shift_right(unsigned(x), 1));
end architecture;
