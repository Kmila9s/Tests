-- G28: concurrent assert / postponed assert, labeled selected and simple assignments.
library ieee; use ieee.std_logic_1164.all;
entity conc_assert is
  port (a, b : in std_logic; sel : in std_logic_vector(1 downto 0); y, z : out std_logic);
end entity;
architecture rtl of conc_assert is
begin
  chk: assert a = '1' report "conc_assert: a is low" severity note;
  postponed assert b = '0' report "conc_assert: b is high" severity note;
  sl: with sel select
    y <= a when "00",
         b when "01",
         '1' when others;
  lz: z <= a xor b;
end architecture;
