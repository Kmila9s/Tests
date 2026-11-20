-- G65: an index or case selector of 10..14 from to_integer.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity lit10 is
  port (addr : in std_logic_vector(3 downto 0); d : out std_logic_vector(7 downto 0);
        c : out std_logic_vector(3 downto 0));
end entity;

architecture rtl of lit10 is
  type rom_t is array (0 to 15) of std_logic_vector(7 downto 0);
  constant rom : rom_t := (x"00", x"11", x"22", x"33", x"44", x"55", x"66", x"77",
                           x"88", x"99", x"AA", x"BB", x"CC", x"DD", x"EE", x"FF");
begin
  d <= rom(to_integer(unsigned(addr)));
  process (addr)
  begin
    case to_integer(unsigned(addr)) is
      when 2 => c <= "0010";
      when 10 => c <= "1010";
      when 12 => c <= "1100";
      when others => c <= "1111";
    end case;
  end process;
end architecture;
