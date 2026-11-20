-- G80: textio input (readline/read) from a file of the project, and report
-- inside a procedure.
library ieee;
use ieee.std_logic_1164.all;
use std.textio.all;

entity textio_in is
  port (y : out std_logic_vector(3 downto 0); n : out integer);
end entity;

architecture sim of textio_in is
  procedure say(constant msg : in string; constant k : in integer) is
  begin
    report "say: " & msg & " k=" & integer'image(k) & ", done";
  end procedure;
begin
  process
    file f : text open read_mode is "textio_in.txt";
    variable l : line;
    variable v : std_logic_vector(3 downto 0);
    variable i : integer;
  begin
    n <= 0;
    while not endfile(f) loop
      readline(f, l);
      read(l, i);
      read(l, v);
      y <= v; n <= i;
      say("row", i);
      wait for 10 ns;
    end loop;
    wait;
  end process;
end architecture;
