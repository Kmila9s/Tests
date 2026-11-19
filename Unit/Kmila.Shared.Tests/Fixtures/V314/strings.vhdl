-- G56: string and character literals with blanks and punctuation.
library ieee; use ieee.std_logic_1164.all;
use std.textio.all;
entity strings is
  port (n : in std_logic_vector(3 downto 0); y : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of strings is
begin
  process
    variable c : character;
    variable l : line;
  begin
    report "strings: a: b";
    report "strings: x;y, z";
    report "strings: say ""hi""";
    report "strings:  two  spaces";
    c := ' ';
    write(l, string'("strings: t = 1, ok"));
    writeline(output, l);
    y <= "0001";
    c := ';';
    wait for 10 ns;
    y <= "0011";
    wait;
  end process;
end architecture;
