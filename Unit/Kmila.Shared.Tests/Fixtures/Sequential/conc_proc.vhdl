-- G28: concurrent procedure calls (unlabeled and labeled) to a declared procedure.
library ieee; use ieee.std_logic_1164.all;
entity conc_proc is
  port (a, b : in std_logic; y, z : out std_logic);
end entity;
architecture rtl of conc_proc is
  procedure drive_and(signal x1, x2 : in std_logic; signal o : out std_logic) is
  begin
    o <= x1 and x2;
  end procedure;
begin
  drive_and(a, b, y);
  lc: drive_and(a, a, z);
end architecture;
