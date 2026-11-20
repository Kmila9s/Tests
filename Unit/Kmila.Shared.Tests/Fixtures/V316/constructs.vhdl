-- G54, G77, G79, G80: constructs that were "not supported yet" or fell
-- back silently.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity leaf is
  generic (W : natural := 2; INIT : std_logic := '0');
  port (a : in std_logic_vector(W - 1 downto 0); y : out std_logic_vector(W - 1 downto 0); z : out std_logic);
end entity;

architecture inv of leaf is
begin
  y <= not a;
  z <= INIT;
end architecture;

architecture buf of leaf is
begin
  y <= a;
  z <= not INIT;
end architecture;

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity constructs is
  generic (SEL : natural := 2);
  port (a : in std_logic_vector(1 downto 0); s : in std_logic_vector(1 downto 0);
        y_init, y_cv, y_sv : out std_logic_vector(3 downto 0);
        y_agg : out std_logic_vector(2 downto 0);
        y_bind : out std_logic_vector(1 downto 0); z_bind : out std_logic;
        y_gen, y_case : out std_logic_vector(1 downto 0));
end entity;

architecture rtl of constructs is
  type mem_t is array (0 to 3) of std_logic_vector(3 downto 0);
  type pair_t is array (0 to 1) of std_logic_vector(1 downto 0);
  function init_mem(base : natural) return mem_t is
    variable m : mem_t;
  begin
    for i in mem_t'range loop
      m(i) := std_logic_vector(to_unsigned(base + i * 3, 4));
    end loop;
    return m;
  end function;
  constant mem : mem_t := init_mem(1);   -- G54: initializer with a user function
  signal p, q, r, t : std_logic;
  component leaf
    generic (W : natural := 2; INIT : std_logic := '0');
    port (a : in std_logic_vector(W - 1 downto 0); y : out std_logic_vector(W - 1 downto 0); z : out std_logic);
  end component;
  for ub : leaf use entity work.leaf(buf) generic map (W => 2, INIT => '1') port map (a => a, y => y, z => z);
begin
  y_init <= mem(to_integer(unsigned(s)));

  -- G77: conditional and selected variable assignments (VHDL-2008)
  process (a, s)
    variable v, w : std_logic_vector(3 downto 0);
  begin
    v := "1111" when s = "11" else "0000" when s = "00" else a & a;
    with s select w := "0001" when "00", "0010" when "01", "0100" when "10", "1000" when others;
    y_cv <= v;
    y_sv <= w;
  end process;

  -- G79: nested aggregate target
  ((p, q), (r, t)) <= pair_t'(a, s);
  y_agg <= p & q & (r xor t);

  -- G80: binding with generic map and port map
  ub: leaf port map (a => a, y => y_bind, z => z_bind);

  -- G80: if generate with elsif/else and case generate with instances
  g_if: if SEL = 1 generate
    u: entity work.leaf(inv) port map (a => a, y => y_gen, z => open);
  elsif SEL = 2 generate
    u: entity work.leaf(buf) port map (a => a, y => y_gen, z => open);
  else generate
    y_gen <= "00";
  end generate;

  g_case: case SEL generate
    when 1 => u: entity work.leaf(buf) port map (a => a, y => y_case, z => open);
    when 2 => u: entity work.leaf(inv) port map (a => a, y => y_case, z => open);
    when others => y_case <= "11";
  end generate;
end architecture;
