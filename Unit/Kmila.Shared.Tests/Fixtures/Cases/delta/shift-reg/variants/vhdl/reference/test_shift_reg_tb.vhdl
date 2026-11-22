-- Testbench for shift register test
-- Shifts in a pattern and verifies delta cycle behavior

library IEEE;
use IEEE.std_logic_1164.all;

entity test_shift_reg_tb is
end test_shift_reg_tb;

architecture test of test_shift_reg_tb is
    signal clk   : std_logic := '0';
    signal din   : std_logic := '1';
    signal dout  : std_logic_vector(3 downto 0);

    constant CLK_PERIOD : time := 10 ns;
begin
    -- Instantiate the DUT
    dut: entity work.test_shift_reg
        port map (
            clk  => clk,
            din  => din,
            dout => dout
        );

    -- Clock generation
    clk_process: process
    begin
        for i in 0 to 7 loop
            clk <= '0';
            wait for CLK_PERIOD / 2;
            clk <= '1';
            wait for CLK_PERIOD / 2;
        end loop;
        wait;
    end process;

    -- Data pattern process
    din_process: process
    begin
        -- Shift in pattern: 1, 0, 1, 1, 0, 0, 1, 0
        din <= '1'; wait for CLK_PERIOD;
        din <= '0'; wait for CLK_PERIOD;
        din <= '1'; wait for CLK_PERIOD;
        din <= '1'; wait for CLK_PERIOD;
        din <= '0'; wait for CLK_PERIOD;
        din <= '0'; wait for CLK_PERIOD;
        din <= '1'; wait for CLK_PERIOD;
        din <= '0'; wait for CLK_PERIOD;
        wait;
    end process;

end test;
