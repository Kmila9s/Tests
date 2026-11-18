-- G27: exit/next with and without label and `when`, in for/while/loop, nested,
-- in a sensitivity-list process (y, z, w) and in a wait-based process (u).
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity exit_next is
  port (n : in std_logic_vector(3 downto 0); y, z, w, u : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of exit_next is
begin
  process (n)
    variable c, k : integer;
  begin
    -- trailing ones from bit 0
    c := 0;
    for i in 0 to 3 loop
      exit when n(i) = '0';
      c := c + 1;
    end loop;
    y <= std_logic_vector(to_unsigned(c, 4));
    -- triangular count, stops at 3
    c := 0;
    outer: for i in 0 to 3 loop
      inner: for j in 0 to 3 loop
        next outer when j > i;
        if n(j) = '1' then
          c := c + 1;
        end if;
        exit outer when c = 3;
      end loop inner;
    end loop outer;
    z <= std_logic_vector(to_unsigned(c, 4));
    -- popcount with while + next, then a bare loop + exit
    k := 0; c := 0;
    while k < 4 loop
      k := k + 1;
      next when n(k - 1) = '0';
      c := c + 1;
    end loop;
    loop
      c := c + 8;
      exit;
    end loop;
    w <= std_logic_vector(to_unsigned(c, 4));
  end process;

  process
    variable c : integer;
  begin
    c := 0;
    scan: loop
      for i in 3 downto 0 loop
        exit scan when n(i) = '1';
        c := c + 1;
      end loop;
      exit;
    end loop scan;
    u <= std_logic_vector(to_unsigned(c, 4));
    wait on n;
  end process;
end architecture;
