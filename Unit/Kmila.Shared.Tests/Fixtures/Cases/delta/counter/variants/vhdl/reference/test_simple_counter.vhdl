-- Simple test file for waveform verification
-- Uses basic integer counter to avoid complex expression parsing issues

library IEEE;
use IEEE.std_logic_1164.all;

entity simple_counter is
    port (
        clk     : in  std_logic;
        reset   : in  std_logic;
        enable  : in  std_logic;
        count   : out std_logic_vector(3 downto 0)
    );
end simple_counter;

architecture Behavioral of simple_counter is
    signal counter_reg : std_logic_vector(3 downto 0) := "0000";
begin
    process(clk)
    begin
        if rising_edge(clk) then
            if reset = '1' then
                counter_reg <= "0000";
            elsif enable = '1' then
                -- Simple manual increment for each bit
                if counter_reg = "0000" then
                    counter_reg <= "0001";
                elsif counter_reg = "0001" then
                    counter_reg <= "0010";
                elsif counter_reg = "0010" then
                    counter_reg <= "0011";
                elsif counter_reg = "0011" then
                    counter_reg <= "0100";
                elsif counter_reg = "0100" then
                    counter_reg <= "0101";
                elsif counter_reg = "0101" then
                    counter_reg <= "0110";
                elsif counter_reg = "0110" then
                    counter_reg <= "0111";
                elsif counter_reg = "0111" then
                    counter_reg <= "1000";
                elsif counter_reg = "1000" then
                    counter_reg <= "1001";
                elsif counter_reg = "1001" then
                    counter_reg <= "1010";
                else
                    counter_reg <= "0000";
                end if;
            end if;
        end if;
    end process;

    count <= counter_reg;
end Behavioral;
