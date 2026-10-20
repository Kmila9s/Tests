library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity hello_scroll is
    port(
        clk     : in  std_logic;
        reset   : in  std_logic;
        step_en : in  std_logic;
        line0   : out std_logic_vector(127 downto 0)
    );
end hello_scroll;

architecture rtl of hello_scroll is
    -- "HELLO KMILA     " — 16 chars × 8 bits, MSB-first (bit 127 = H).
    -- Encoded as one 128-bit literal so the interpreter doesn't have to
    -- unroll a long concat chain at parse time.
    signal reg : std_logic_vector(127 downto 0) :=
        "01001000010001010100110001001100010011110010000001001011010011010100100101001100010000010010000000100000001000000010000000100000";
begin
    process(clk, reset)
    begin
        if reset = '1' then
            reg <= "01001000010001010100110001001100010011110010000001001011010011010100100101001100010000010010000000100000001000000010000000100000";
        elsif rising_edge(clk) then
            if step_en = '1' then
                -- Rotate left by 8 bits — move the leftmost character to
                -- the tail so the banner appears to scroll left.
                reg <= reg(119 downto 0) & reg(127 downto 120);
            end if;
        end if;
    end process;

    line0 <= reg;
end rtl;
