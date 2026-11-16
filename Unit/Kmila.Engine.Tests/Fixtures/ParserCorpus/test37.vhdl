-- Test 37: 16-bit PWM Generator with duty-cycle input
-- Curriculum: Sistemas en Chip, Unit 2.3 (Timers/PWM)

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity PWMGen is
    generic (PERIOD : integer := 1000);
    port (
        clk    : in  std_logic;
        rst    : in  std_logic;
        duty   : in  std_logic_vector(15 downto 0);
        pwm    : out std_logic
    );
end PWMGen;

architecture RTL of PWMGen is
    signal counter : unsigned(15 downto 0) := (others => '0');
    signal duty_u  : unsigned(15 downto 0);
begin
    duty_u <= unsigned(duty);

    cnt_proc : process(clk, rst)
    begin
        if rst = '1' then
            counter <= (others => '0');
        elsif rising_edge(clk) then
            if counter = PERIOD - 1 then
                counter <= (others => '0');
            else
                counter <= counter + 1;
            end if;
        end if;
    end process cnt_proc;

    pwm <= '1' when counter < duty_u else '0';
end RTL;
