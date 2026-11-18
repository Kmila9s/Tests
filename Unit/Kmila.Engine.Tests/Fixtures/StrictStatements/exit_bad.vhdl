-- G27: exit naming a label that is not an enclosing loop (GHDL rejects).
library ieee; use ieee.std_logic_1164.all;
entity exit_bad is
  port (a : in std_logic; y : out std_logic);
end entity;
architecture rtl of exit_bad is
begin
  process (a)
  begin
    y <= a;
    l1: for i in 0 to 1 loop
      null;
    end loop l1;
    for i in 0 to 1 loop
      exit l1;
    end loop;
  end process;
end architecture;
