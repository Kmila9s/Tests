-- G12: array (1 to 4) of record; element/field writes and dynamic-index field read m(i).lo.
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity arr_rec is
  port (a, b : in std_logic; sel : in std_logic_vector(1 downto 0); y : out std_logic_vector(1 downto 0));
end entity;
architecture rtl of arr_rec is
  type pair_t is record
    lo, hi : std_logic;
  end record;
  type parr_t is array (1 to 4) of pair_t;
  signal m : parr_t;
  signal i : integer range 1 to 4;
begin
  m(1)    <= (lo => a, hi => b);
  m(2).lo <= b;
  m(2).hi <= a;
  m(3)    <= (lo => '1', hi => '0');
  m(4)    <= (others => '0');
  i <= to_integer(unsigned(sel)) + 1;
  y <= m(i).hi & m(i).lo;
end architecture;
