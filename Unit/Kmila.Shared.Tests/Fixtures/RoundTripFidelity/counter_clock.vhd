-- Kmila example: 2-digit decimal counter (00 → 99 → 00 + overflow pulse).
-- Showcases: two 7-segment displays in decoded-decimal mode + an
-- overflow LED on rollover. Try setting a breakpoint on the rollover
-- branch to watch the FSM transition step by step.
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

entity counter_clock is
  port (
    clk      : in  STD_LOGIC;
    reset    : in  STD_LOGIC;
    tick     : in  STD_LOGIC;
    units    : out STD_LOGIC_VECTOR(3 downto 0);
    tens     : out STD_LOGIC_VECTOR(3 downto 0);
    overflow : out STD_LOGIC
  );
end entity counter_clock;

architecture behavioral of counter_clock is
  signal u_q  : unsigned(3 downto 0) := (others => '0');
  signal t_q  : unsigned(3 downto 0) := (others => '0');
  signal ov_q : STD_LOGIC := '0';
begin
  process(clk, reset)
  begin
    if reset = '1' then
      u_q  <= (others => '0');
      t_q  <= (others => '0');
      ov_q <= '0';
    elsif rising_edge(clk) then
      ov_q <= '0';
      if tick = '1' then
        if u_q = 9 then
          u_q <= (others => '0');
          if t_q = 9 then
            t_q  <= (others => '0');
            ov_q <= '1';
          else
            t_q <= t_q + 1;
          end if;
        else
          u_q <= u_q + 1;
        end if;
      end if;
    end if;
  end process;

  units    <= std_logic_vector(u_q);
  tens     <= std_logic_vector(t_q);
  overflow <= ov_q;
end architecture behavioral;
