-- Kmila example: 3-state Moore traffic-light FSM.
-- Showcases: enum state + clocked transition + concurrent output
-- decoding. State machine wakeup every 10 clock cycles when ENABLE is
-- on. Set a breakpoint inside the `case st is` block to watch state
-- transitions in step-replay mode.
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

entity traffic_light is
  port (
    clk       : in  STD_LOGIC;
    reset     : in  STD_LOGIC;
    enable    : in  STD_LOGIC;
    red       : out STD_LOGIC;
    yellow    : out STD_LOGIC;
    green     : out STD_LOGIC;
    state_out : out STD_LOGIC_VECTOR(1 downto 0)
  );
end entity traffic_light;

architecture behavioral of traffic_light is
  type state_t is (S_RED, S_GREEN, S_YELLOW);
  signal st  : state_t := S_RED;
  signal ctr : unsigned(3 downto 0) := (others => '0');
begin
  process(clk, reset)
  begin
    if reset = '1' then
      st  <= S_RED;
      ctr <= (others => '0');
    elsif rising_edge(clk) then
      if enable = '1' then
        if ctr = 9 then
          ctr <= (others => '0');
          case st is
            when S_RED    => st <= S_GREEN;
            when S_GREEN  => st <= S_YELLOW;
            when S_YELLOW => st <= S_RED;
          end case;
        else
          ctr <= ctr + 1;
        end if;
      end if;
    end if;
  end process;

  red       <= '1' when st = S_RED    else '0';
  yellow    <= '1' when st = S_YELLOW else '0';
  green     <= '1' when st = S_GREEN  else '0';
  state_out <= "00" when st = S_RED else
               "01" when st = S_GREEN else "10";
end architecture behavioral;
