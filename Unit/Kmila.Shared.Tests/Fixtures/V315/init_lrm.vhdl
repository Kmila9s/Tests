-- v3.1.5 G39: LRM initial values. Undeclared std_logic/vector start at 'U',
-- integer at integer'left, a range at its left bound, natural at 0, an enum
-- at its first literal, boolean FALSE, bit '0'; a process variable starts at
-- 'U'; an input port left open takes its default; a register without reset
-- stays 'U' (and a counter without reset becomes X).
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity init_child is
  port (k : in std_logic := '1'; o : out std_logic);
end entity;

architecture rtl of init_child is
begin
  o <= k;
end architecture;

library ieee;
use ieee.std_logic_1164.all;

entity init_lrm is
  port (
    clk, d : in std_logic;
    q, s0, pv, pd, en, bo, bt : out std_logic;
    cnt : out std_logic_vector(3 downto 0);
    vi : out std_logic_vector(3 downto 0);
    iv : out std_logic_vector(31 downto 0);
    ir : out std_logic_vector(2 downto 0);
    nt : out std_logic_vector(3 downto 0)
  );
end entity;

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

architecture rtl of init_lrm is
  type state_t is (idle, run, done);
  signal st : state_t;
  signal s : std_logic;
  signal v : std_logic_vector(3 downto 0);
  signal c : unsigned(3 downto 0);
  signal i : integer;
  signal r : integer range -4 to 3;
  signal n : natural;
  signal b : boolean;
  signal t : bit;
begin
  process (clk)
  begin
    if rising_edge(clk) then
      q <= d;
      c <= c + 1;
    end if;
  end process;
  process (d)
    variable w : std_logic;
  begin
    pv <= w;
  end process;
  u0: entity work.init_child port map (k => open, o => pd);
  s0 <= s;
  vi <= v;
  cnt <= std_logic_vector(c);
  en <= '1' when st = idle else '0';
  bo <= '1' when b else '0';
  bt <= to_stdulogic(t);
  iv <= std_logic_vector(to_signed(i, 32));
  ir <= std_logic_vector(to_signed(r, 3));
  nt <= std_logic_vector(to_unsigned(n, 4));
end architecture;
