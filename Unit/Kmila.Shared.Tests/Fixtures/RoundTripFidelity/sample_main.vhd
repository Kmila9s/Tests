-- Kmila Demo Board: every visual widget at once.
--
-- Open the editor and try each surface:
--   • Code tab        — read + edit this file
--   • Blocks tab      — same design as draggable blocks
--   • Step replay     — set a breakpoint on the cnt_q increment line
--                       below and step through clk cycles
--   • Visual tab      — pre-wired canvas: Switch RUN, Button NUDGE,
--                       Stepper rotor, LED Array (count), LED HB
--                       (heartbeat). Hit ▶ Run and play.

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

entity demo_board is
  port (
    -- Inputs (driven by visual widgets on the Visual tab)
    clk       : in  STD_LOGIC;
    reset     : in  STD_LOGIC;
    run       : in  STD_LOGIC;
    dir       : in  STD_LOGIC;
    nudge     : in  STD_LOGIC;

    -- Outputs (read by visual widgets on the Visual tab)
    heartbeat : out STD_LOGIC;
    count     : out STD_LOGIC_VECTOR(3 downto 0);
    btn_echo  : out STD_LOGIC;

    -- Stepper Gray-coded phase outputs (4 pins, drive the Stepper widget)
    phase_a   : out STD_LOGIC;
    phase_b   : out STD_LOGIC;
    phase_an  : out STD_LOGIC;
    phase_bn  : out STD_LOGIC
  );
end entity demo_board;

architecture behavioral of demo_board is
  -- Internal state: 4-bit display counter + 3-bit stepper index + heartbeat.
  signal cnt_q  : unsigned(3 downto 0) := (others => '0');
  signal step_q : unsigned(2 downto 0) := (others => '0');
  signal hb_q   : STD_LOGIC := '0';
begin
  -- Sync block: asynchronous reset, then advance on every clk rising edge
  -- whenever the RUN switch is on. DIR picks the stepper direction.
  process(clk, reset)
  begin
    if reset = '1' then
      cnt_q  <= (others => '0');
      step_q <= (others => '0');
      hb_q   <= '0';
    elsif rising_edge(clk) then
      if run = '1' then
        cnt_q <= cnt_q + 1;
        hb_q  <= not hb_q;
        if dir = '1' then
          step_q <= step_q + 1;
        else
          step_q <= step_q - 1;
        end if;
      end if;
    end if;
  end process;

  -- Output wiring.
  heartbeat <= hb_q;
  count     <= std_logic_vector(cnt_q);
  btn_echo  <= nudge;

  -- 4-bit Gray-code stepper decoder. The 8 patterns match the Stepper
  -- widget's internal table exactly so the rotor advances one full step
  -- per clk rising edge (when RUN is on).
  --   idx  pattern (A B An Bn)
  --   0    1 0 0 0
  --   1    1 1 0 0
  --   2    0 1 0 0
  --   3    0 1 1 0
  --   4    0 0 1 0
  --   5    0 0 1 1
  --   6    0 0 0 1
  --   7    1 0 0 1
  phase_a  <= '1' when step_q = "000" or step_q = "001" or step_q = "111" else '0';
  phase_b  <= '1' when step_q = "001" or step_q = "010" or step_q = "011" else '0';
  phase_an <= '1' when step_q = "011" or step_q = "100" or step_q = "101" else '0';
  phase_bn <= '1' when step_q = "101" or step_q = "110" or step_q = "111" else '0';
end architecture behavioral;
