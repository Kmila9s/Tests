-- G12: ascending arrays of std_logic and of vectors, bounds from a constant, named aggregate.
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity arr_asc is
  port (a : in std_logic; sel : in std_logic_vector(1 downto 0);
        y : out std_logic_vector(3 downto 0); z : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of arr_asc is
  constant N : integer := 3;
  type bits_t is array (0 to 7) of std_logic;
  type nib_t is array (1 to N) of std_logic_vector(1 downto 0);
  signal b8 : bits_t;
  signal r  : nib_t;
begin
  b8 <= (0 => a, 6 => '1', others => '0');
  r  <= ("01", "10", "11");
  y  <= b8(0) & b8(1) & b8(6) & b8(to_integer(unsigned(sel)));
  z  <= r(1) & r(N);
end architecture;
