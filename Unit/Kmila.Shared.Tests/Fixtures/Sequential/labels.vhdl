-- G27: labels on sequential statements (assignment, if, case, loop) and on end clauses.
library ieee; use ieee.std_logic_1164.all;
entity labels is
  port (a, b : in std_logic; n : in std_logic_vector(3 downto 0); y : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of labels is
begin
  main: process (a, b, n)
    variable v : std_logic_vector(3 downto 0);
  begin
    s1: v := n;
    chk: if a = '1' then
      v(0) := b;
    end if chk;
    sw: case b is
      when '1' => v(3) := '1';
      when others => null;
    end case sw;
    lp: for i in 1 to 2 loop
      v(i) := a;
    end loop lp;
    asg: y <= v;
  end process main;
end architecture;
