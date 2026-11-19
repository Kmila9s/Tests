-- G24: `u : component c [generic map …] port map …`.
library ieee; use ieee.std_logic_1164.all;
entity cinv is port (i : in std_logic; o : out std_logic); end entity;
architecture rtl of cinv is begin o <= not i; end architecture;

library ieee; use ieee.std_logic_1164.all;
entity cconst is generic (V : std_logic_vector(3 downto 0) := "0000"); port (y : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of cconst is begin y <= V; end architecture;

library ieee; use ieee.std_logic_1164.all;
entity comp_kw is port (a : in std_logic; o : out std_logic; y : out std_logic_vector(3 downto 0)); end entity;
architecture rtl of comp_kw is
  component cinv is port (i : in std_logic; o : out std_logic); end component;
  component cconst is generic (V : std_logic_vector(3 downto 0) := "0000"); port (y : out std_logic_vector(3 downto 0)); end component;
begin
  u1 : component cinv port map (i => a, o => o);
  u2 : component cconst generic map (V => "1001") port map (y => y);
end architecture;
