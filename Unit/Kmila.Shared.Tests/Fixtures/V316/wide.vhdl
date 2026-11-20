-- G44: arithmetic and comparison wider than 32 bits, and to_integer at 31/32 bits.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity wide is
  port (x40, y40 : in std_logic_vector(39 downto 0);
        x64 : in std_logic_vector(63 downto 0);
        x96 : in std_logic_vector(95 downto 0);
        s40 : out std_logic_vector(39 downto 0);
        p80 : out std_logic_vector(79 downto 0);
        lt40 : out std_logic;
        s64 : out std_logic_vector(63 downto 0);
        s96 : out std_logic_vector(95 downto 0);
        neg64 : out std_logic_vector(63 downto 0);
        i31 : out integer);
end entity;

architecture rtl of wide is
begin
  s40  <= std_logic_vector(unsigned(x40) + unsigned(y40));
  p80  <= std_logic_vector(unsigned(x40) * unsigned(y40));
  lt40 <= '1' when unsigned(x40) < unsigned(y40) else '0';
  s64  <= std_logic_vector(unsigned(x64) + 1);
  s96  <= std_logic_vector(unsigned(x96) + unsigned(x96));
  neg64 <= std_logic_vector(-signed(x64));
  i31  <= to_integer(unsigned(x40(30 downto 0)));
end architecture;
