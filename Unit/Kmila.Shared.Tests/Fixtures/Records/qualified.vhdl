-- G14: a selected name (work.pkg.c) inside an instance whose body has a local signal named c.
library ieee; use ieee.std_logic_1164.all;
package qual_pkg is
  constant c : std_logic_vector(3 downto 0) := "1010";
end package;

library ieee; use ieee.std_logic_1164.all;
entity qual_child is
  port (x : in std_logic_vector(3 downto 0); y : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of qual_child is
  signal c : std_logic_vector(3 downto 0);
begin
  c <= x xor work.qual_pkg.c;
  y <= c;
end architecture;

library ieee; use ieee.std_logic_1164.all;
entity qualified is
  port (x : in std_logic_vector(3 downto 0); y : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of qualified is
begin
  u1 : entity work.qual_child port map (x => x, y => y);
end architecture;
