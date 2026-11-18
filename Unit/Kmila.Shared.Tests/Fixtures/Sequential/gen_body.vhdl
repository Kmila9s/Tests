-- G29: generate bodies with an entity instance, an assert and a procedure call.
library ieee; use ieee.std_logic_1164.all;
entity gen_inv is
  port (i : in std_logic; o : out std_logic);
end entity;
architecture rtl of gen_inv is
begin
  o <= not i;
end architecture;

library ieee; use ieee.std_logic_1164.all;
entity gen_body is
  port (n : in std_logic_vector(3 downto 0); y : out std_logic_vector(3 downto 0); z : out std_logic);
end entity;
architecture rtl of gen_body is
  procedure copy(signal i : in std_logic; signal o : out std_logic) is
  begin
    o <= i;
  end procedure;
begin
  g: for k in 0 to 3 generate
    u: entity work.gen_inv port map (i => n(k), o => y(k));
    assert n(k) = '0' or k /= 3 report "gen_body: msb set" severity note;
  end generate;
  g2: for k in 0 to 0 generate
    copy(n(1), z);
  end generate;
end architecture;
