-- Phase 2 visual-runtime fixture. Drives a 4-bit Gray-coded full-step
-- pattern on four separate single-bit ports so the wiring modal can
-- bind them 1:1 to StepperWidget pins (phaseA / phaseB / phaseAn / phaseBn).
-- Internal 3-bit counter advances on every rising_edge(clk); the four
-- outputs derive their bit from the canonical 8-pattern table via
-- concurrent conditional assignment (parser-portable across Kmila +
-- vendor sims).
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity stepper_phases is
    port (
        clk    : in  std_logic;
        a_out  : out std_logic;
        b_out  : out std_logic;
        an_out : out std_logic;
        bn_out : out std_logic
    );
end entity;

architecture behavioral of stepper_phases is
    signal idx : unsigned(2 downto 0) := (others => '0');
begin
    process(clk)
    begin
        if rising_edge(clk) then
            idx <= idx + 1;
        end if;
    end process;

    -- Canonical 8-pattern table (idx -> 4-bit Gray):
    --   0:1000  1:1100  2:0100  3:0110  4:0010  5:0011  6:0001  7:1001
    a_out  <= '1' when idx = "000" or idx = "001" or idx = "111" else '0';
    b_out  <= '1' when idx = "001" or idx = "010" or idx = "011" else '0';
    an_out <= '1' when idx = "011" or idx = "100" or idx = "101" else '0';
    bn_out <= '1' when idx = "101" or idx = "110" or idx = "111" else '0';
end architecture;
