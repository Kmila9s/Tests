-- G31: textio output (write/hwrite/writeline to output).
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
use std.textio.all;
entity textio_out is
  port (a : in std_logic; n : in std_logic_vector(3 downto 0));
end entity;
architecture sim of textio_out is
begin
  process (a, n)
    variable l : line;
  begin
    write(l, string'("textio_out: n="));
    write(l, to_integer(unsigned(n)));
    write(l, string'(" a="));
    write(l, a);
    write(l, string'(" bits="));
    write(l, n);
    write(l, string'(" hex="));
    hwrite(l, n);
    writeline(output, l);
  end process;
end architecture;
