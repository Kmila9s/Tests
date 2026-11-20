-- G43: several drivers on resolved signals (IEEE 1164 resolution).
-- Inputs a, b, ea, eb come from the testbench; the design drives its outputs
-- from more than one process or concurrent statement.
library ieee;
use ieee.std_logic_1164.all;

entity drivers is
  port (a, b, ea, eb : in std_logic;
        y2  : out std_logic;                     -- two concurrent drivers
        bus_t : out std_logic;                   -- two tri-state buffers
        lh  : out std_logic;                     -- 'L' and 'H'
        v   : out std_logic_vector(3 downto 0);  -- disjoint bits from two processes
        w   : out std_logic_vector(3 downto 0);  -- dynamic index (whole) + one bit
        k   : out std_logic_vector(1 downto 0)); -- partial overlap
end entity;

architecture rtl of drivers is
begin
  y2 <= a;
  y2 <= b;

  bus_t <= a when ea = '1' else 'Z';
  bus_t <= b when eb = '1' else 'Z';

  lh <= 'L';
  lh <= 'H';

  p_hi: process (a) begin v(3 downto 2) <= a & a; end process;
  p_lo: process (b) begin v(1 downto 0) <= b & '0'; end process;

  -- A loop index is not static: this process drives every element of w,
  -- though it only assigns w(1..3); w(0) keeps its driver's initial 'U'.
  p_dyn: process (a)
  begin
    for i in 1 to 3 loop
      w(i) <= a;
    end loop;
  end process;
  w(0) <= b;

  k <= a & 'Z';
  k(0) <= b;
end architecture;
