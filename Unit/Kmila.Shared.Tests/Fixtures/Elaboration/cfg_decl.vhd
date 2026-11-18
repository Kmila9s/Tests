-- G20: configuration declarations (nested block configuration, `use configuration`), a configuration as the top.
library ieee; use ieee.std_logic_1164.all;
entity foo is port (y : out std_logic_vector(3 downto 0)); end entity;
architecture arch_a of foo is begin y <= "1010"; end architecture;
architecture arch_b of foo is begin y <= "0101"; end architecture;

library ieee; use ieee.std_logic_1164.all;
entity mid is port (y : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of mid is
  component foo is port (y : out std_logic_vector(3 downto 0)); end component;
begin
  m1: foo port map (y => y);
end architecture;

library ieee; use ieee.std_logic_1164.all;
entity cfg_decl is port (o1, o2 : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of cfg_decl is
  component mid is port (y : out std_logic_vector(3 downto 0)); end component;
begin
  t1: mid port map (y => o1);
  t2: mid port map (y => o2);
end architecture;

configuration cfg_mid_a of mid is
  for rtl
    for m1 : foo use entity work.foo(arch_a); end for;
  end for;
end configuration cfg_mid_a;

configuration cfg_top of cfg_decl is
  for rtl
    for t1 : mid use configuration work.cfg_mid_a; end for;
    for t2 : mid
      use entity work.mid(rtl);
      for rtl
        for m1 : foo use entity work.foo(arch_b); end for;
      end for;
    end for;
  end for;
end cfg_top;
