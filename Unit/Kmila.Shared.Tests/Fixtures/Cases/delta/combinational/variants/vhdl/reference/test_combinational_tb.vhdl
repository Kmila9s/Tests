-- Testbench for combinational logic test
-- Tests all input combinations for AND, OR, XOR, NOT

library IEEE;
use IEEE.std_logic_1164.all;

entity test_combinational_tb is
end test_combinational_tb;

architecture test of test_combinational_tb is
    signal a      : std_logic := '0';
    signal b      : std_logic := '0';
    signal y_and  : std_logic;
    signal y_or   : std_logic;
    signal y_xor  : std_logic;
    signal y_not  : std_logic;

    constant DELAY : time := 10 ns;
begin
    -- Instantiate the DUT
    dut: entity work.test_combinational
        port map (
            a     => a,
            b     => b,
            y_and => y_and,
            y_or  => y_or,
            y_xor => y_xor,
            y_not => y_not
        );

    -- Stimulus process
    stim_process: process
    begin
        -- Test all input combinations
        -- 00
        a <= '0'; b <= '0';
        wait for DELAY;

        -- 01
        a <= '0'; b <= '1';
        wait for DELAY;

        -- 10
        a <= '1'; b <= '0';
        wait for DELAY;

        -- 11
        a <= '1'; b <= '1';
        wait for DELAY;

        wait;
    end process;

end test;
