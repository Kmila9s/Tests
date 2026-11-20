-- Testbenches for int_range and len_err: go rises at 10 ns.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity tb_ovf is
end entity;

architecture sim of tb_ovf is
  signal go : std_logic := '0';
  signal y : integer;
begin
  dut: entity work.int_ovf port map (go => go, y => y);
  go <= '1' after 10 ns;
end architecture;

library ieee;
use ieee.std_logic_1164.all;

entity tb_nat is
end entity;

architecture sim of tb_nat is
  signal go : std_logic := '0';
  signal y : natural;
begin
  dut: entity work.nat_neg port map (go => go, y => y);
  go <= '1' after 10 ns;
end architecture;

library ieee;
use ieee.std_logic_1164.all;

entity tb_sub is
end entity;

architecture sim of tb_sub is
  signal go : std_logic := '0';
  signal y : integer range 0 to 9;
begin
  dut: entity work.sub_rng port map (go => go, y => y);
  go <= '1' after 10 ns;
end architecture;

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity tb_len is
end entity;

architecture sim of tb_len is
  signal a, y : unsigned(3 downto 0);
begin
  dut: entity work.len_err port map (a => a, y => y);
  a <= "0001", "0010" after 10 ns;
end architecture;
