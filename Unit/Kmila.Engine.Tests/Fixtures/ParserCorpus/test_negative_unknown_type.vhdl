-- Negative fixture for Parser strictness work (2026-05-17, Task 3).
-- Expected behaviour: PortsParser must throw a ParserException because
-- `STDLOGIC` is one edit away from the canonical `std_logic`. Before the
-- strictness fix the Parser accepted this silently and the Debugger
-- reported "builds fine" — see [[project_vhdl_parser_too_lenient]].
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity ripple_typo is
  port (
    clk    : in  STD_LOGIC;
    reset  : in  STD_LOGIC;
    enable : in  STD_LOGIC;
    bit0   : out STD_LOGIC;
    bit1   : out STDLOGIC;
    bit2   : out STD_LOGIC
  );
end entity ripple_typo;

architecture behavioral of ripple_typo is
  signal q0, q1 : STD_LOGIC := '0';
begin
  process(clk, reset)
  begin
    if reset = '1' then
      q0 <= '0'; q1 <= '0';
    elsif rising_edge(clk) then
      if enable = '1' then
        q0 <= not q0;
        q1 <= q0;
      end if;
    end if;
  end process;
  bit0 <= q0;
  bit1 <= q1;
  bit2 <= q0;
end architecture behavioral;
