library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity plus_sign_scroller is
    port(
        clk     : in  std_logic;
        reset   : in  std_logic;
        step_en : in  std_logic;
        pattern : out std_logic_vector(8 downto 0)
    );
end plus_sign_scroller;

architecture rtl of plus_sign_scroller is
    -- Six frames of a plus-sign scrolling one column at a time. Each
    -- 9-bit word is row-major, LSB = (0,0). "010 111 010" is a
    -- centered '+' with all four arms lit.
    type frame_t is array(0 to 5) of std_logic_vector(8 downto 0);
    constant frames : frame_t := (
        "000000000",
        "001010001",
        "010101010",
        "111111111",
        "010111010",   -- centered plus
        "010010010"
    );
    signal idx : integer range 0 to 5 := 0;
begin
    process(clk, reset)
    begin
        if reset = '1' then
            idx <= 0;
        elsif rising_edge(clk) then
            if step_en = '1' then
                if idx = 5 then idx <= 0; else idx <= idx + 1; end if;
            end if;
        end if;
    end process;

    pattern <= frames(idx);
end rtl;
