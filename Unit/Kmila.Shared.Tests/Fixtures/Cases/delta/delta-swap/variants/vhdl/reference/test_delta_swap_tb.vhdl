-- Testbench for delta swap test
-- Verifies VHDL delta cycle semantics for signal swapping

library IEEE;
use IEEE.std_logic_1164.all;

entity test_delta_swap_tb is
end test_delta_swap_tb;

architecture test of test_delta_swap_tb is
    signal clk    : std_logic := '0';
    signal swap   : std_logic := '1';
    signal a_out  : std_logic;
    signal b_out  : std_logic;

    constant CLK_PERIOD : time := 10 ns;
begin
    -- Instantiate the DUT
    dut: entity work.test_delta_swap
        port map (
            clk   => clk,
            swap  => swap,
            a_out => a_out,
            b_out => b_out
        );

    -- Clock generation
    clk_process: process
    begin
        for i in 0 to 4 loop
            clk <= '0';
            wait for CLK_PERIOD / 2;
            clk <= '1';
            wait for CLK_PERIOD / 2;
        end loop;
        wait;
    end process;

end test;
