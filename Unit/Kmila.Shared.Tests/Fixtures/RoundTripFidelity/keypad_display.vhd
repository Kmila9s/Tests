library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity keypad_display is
    port(
        clk     : in  std_logic;
        reset   : in  std_logic;
        key     : in  std_logic_vector(3 downto 0);
        pressed : in  std_logic;
        display : out std_logic_vector(3 downto 0)
    );
end keypad_display;

architecture rtl of keypad_display is
    signal reg : std_logic_vector(3 downto 0) := "0000";
begin
    process(clk, reset)
    begin
        if reset = '1' then
            reg <= "0000";
        elsif rising_edge(clk) then
            if pressed = '1' then
                reg <= key;
            end if;
        end if;
    end process;

    display <= reg;
end rtl;
