-- G57: instances inside for/if generate, generate-local signals, nested generate with a generic range.
library ieee; use ieee.std_logic_1164.all;
entity gi_inv is port (i : in std_logic; o : out std_logic); end entity;
architecture rtl of gi_inv is begin o <= not i; end architecture;

library ieee; use ieee.std_logic_1164.all;
entity gi_buf is
  generic (W : natural := 2);
  port (i : in std_logic_vector(W-1 downto 0); o : out std_logic_vector(W-1 downto 0));
end entity;
architecture rtl of gi_buf is
begin
  g: for j in 0 to W-1 generate
    u: entity work.gi_inv port map (i => i(j), o => o(j));
  end generate;
end architecture;

library ieee; use ieee.std_logic_1164.all;
entity gen_inst is
  generic (WIDTH : natural := 4);
  port (n : in std_logic_vector(3 downto 0);
        y, w : out std_logic_vector(3 downto 0);
        z : out std_logic_vector(1 downto 0);
        e : out std_logic);
end entity;
architecture rtl of gen_inst is
  constant USE_E : boolean := true;
begin
  g1: for k in 0 to WIDTH-1 generate
    signal t : std_logic;
  begin
    u: entity work.gi_inv port map (i => n(k), o => t);
    y(k) <= t xor n(0);
  end generate;
  g2: for k in WIDTH-1 downto 0 generate
    g3: if k mod 2 = 0 generate
      u: entity work.gi_inv port map (i => n(k), o => w(k));
    end generate;
    g4: if k mod 2 = 1 generate
      w(k) <= n(k);
    end generate;
  end generate;
  ub: entity work.gi_buf generic map (W => 2) port map (i => n(3 downto 2), o => z);
  ge: if USE_E generate
    ue: entity work.gi_inv port map (i => n(0), o => e);
  end generate;
end architecture;
