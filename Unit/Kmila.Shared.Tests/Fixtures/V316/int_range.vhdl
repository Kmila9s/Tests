-- G66: integer overflow and subtype range violations are run-time errors.
library ieee;
use ieee.std_logic_1164.all;

entity int_ovf is
  port (go : in std_logic; y : out integer);
end entity;

architecture rtl of int_ovf is
begin
  process (go)
    variable v : integer := 2147483647;
  begin
    if go = '1' then
      v := v + 1;
    end if;
    y <= v;
  end process;
end architecture;

library ieee;
use ieee.std_logic_1164.all;

entity nat_neg is
  port (go : in std_logic; y : out natural);
end entity;

architecture rtl of nat_neg is
begin
  process (go)
    variable n : natural := 1;
  begin
    if go = '1' then
      n := n - 2;
    end if;
    y <= n;
  end process;
end architecture;

library ieee;
use ieee.std_logic_1164.all;

entity sub_rng is
  port (go : in std_logic; y : out integer range 0 to 9);
end entity;

architecture rtl of sub_rng is
  signal c : integer range 0 to 9 := 8;
begin
  process (go)
  begin
    if go = '1' then
      c <= c + 2;
    end if;
  end process;
  y <= c;
end architecture;
