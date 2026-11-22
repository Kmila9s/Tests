-- Testbench for simple_counter
-- This drives the clock and inputs for simulation

library IEEE;
use IEEE.std_logic_1164.all;

entity simple_counter_tb is
end simple_counter_tb;

architecture test of simple_counter_tb is
    signal clk     : std_logic := '0';
    signal reset   : std_logic := '0';
    signal enable  : std_logic := '1';
    signal count   : std_logic_vector(3 downto 0);

    constant CLK_PERIOD : time := 10 ns;
begin
    -- Instantiate the DUT
    dut: entity work.simple_counter
        port map (
            clk    => clk,
            reset  => reset,
            enable => enable,
            count  => count
        );

    -- Clock generation
    clk_process: process
    begin
        for i in 0 to 9 loop  -- 10 clock cycles
            clk <= '0';
            wait for CLK_PERIOD / 2;
            clk <= '1';
            wait for CLK_PERIOD / 2;
        end loop;
        wait;
    end process;

end test;
