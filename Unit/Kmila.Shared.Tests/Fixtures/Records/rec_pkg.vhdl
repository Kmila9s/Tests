-- G11: record and enum declared in a package, record-typed ports, field bit/slice access.
library ieee; use ieee.std_logic_1164.all;
package rec_pkg_types is
  type mode_t is (m_off, m_on);
  type cfg_t is record
    en   : std_logic;
    mode : mode_t;
    d    : std_logic_vector(3 downto 0);
  end record;
end package;

library ieee; use ieee.std_logic_1164.all;
use work.rec_pkg_types.all;
entity rec_pkg is
  port (ci : in cfg_t; co : out cfg_t; y : out std_logic; s : out std_logic_vector(1 downto 0));
end entity;
architecture rtl of rec_pkg is
begin
  co.en   <= not ci.en;
  co.mode <= m_off when ci.mode = m_on else m_on;
  co.d    <= ci.d(0) & ci.d(3 downto 1);
  y <= ci.d(2);
  s <= ci.d(3 downto 2);
end architecture;

library ieee; use ieee.std_logic_1164.all;
use work.rec_pkg_types.all;
entity rec_pkg_top is
  port (a : in std_logic; din : in std_logic_vector(3 downto 0);
        en_o, on_o, y : out std_logic; d_o : out std_logic_vector(3 downto 0); s : out std_logic_vector(1 downto 0));
end entity;
architecture rtl of rec_pkg_top is
  signal c_in, c_out : cfg_t;
begin
  c_in.en   <= a;
  c_in.mode <= m_on when a = '1' else m_off;
  c_in.d    <= din;
  u : entity work.rec_pkg port map (ci => c_in, co => c_out, y => y, s => s);
  en_o <= c_out.en;
  on_o <= '1' when c_out.mode = m_on else '0';
  d_o  <= c_out.d;
end architecture;
