-- G53/G70: inout ports. A bidirectional cell drives its pad when oe = '1'
-- and reads it back; the top connects two cells to one pad signal and
-- exposes it as an inout port too.
library ieee;
use ieee.std_logic_1164.all;

entity bidir_cell is
  port (oe, d : in std_logic; pad : inout std_logic; q : out std_logic);
end entity;

architecture rtl of bidir_cell is
begin
  pad <= d when oe = '1' else 'Z';
  q <= pad;
end architecture;

library ieee;
use ieee.std_logic_1164.all;

entity inout_bus is
  port (oe1, d1, oe2, d2 : in std_logic;
        pad : inout std_logic;
        q1, q2 : out std_logic;
        bus4 : inout std_logic_vector(3 downto 0);
        q4 : out std_logic);
end entity;

architecture rtl of inout_bus is
  signal unused : std_logic;
begin
  c1: entity work.bidir_cell port map (oe => oe1, d => d1, pad => pad, q => q1);
  c2: entity work.bidir_cell port map (oe => oe2, d => d2, pad => pad, q => q2);
  -- an inout formal on one element of a vector port
  c3: entity work.bidir_cell port map (oe => oe1, d => d2, pad => bus4(2), q => q4);
  -- an out formal left open must not drive anything in the parent
  c4: entity work.bidir_cell port map (oe => oe2, d => d1, pad => unused, q => open);
end architecture;
