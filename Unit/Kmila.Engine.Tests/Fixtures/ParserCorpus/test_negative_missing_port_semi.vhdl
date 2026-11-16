-- Negative fixture for Parser strictness work (2026-05-17, Task 3).
-- Expected behaviour: PortsParser must throw a ParserException because
-- the `bit2 : out STD_LOGIC` line is missing its trailing `;` before
-- the next port `bit3` begins. Before the strictness fix this cascaded
-- into a confusing "expected )" error far below the actual problem.
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity ripple_no_semi is
  port (
    clk    : in  STD_LOGIC;
    bit0   : out STD_LOGIC;
    bit1   : out STD_LOGIC;
    bit2   : out STD_LOGIC
    bit3   : out STD_LOGIC
  );
end entity ripple_no_semi;

architecture behavioral of ripple_no_semi is
begin
  bit0 <= '0';
  bit1 <= '0';
  bit2 <= '0';
  bit3 <= '0';
end architecture behavioral;
