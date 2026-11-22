-- test_shift_reg.vhdl
-- Tests a 4-bit shift register (sequential logic with delta cycles)

library IEEE;
use IEEE.std_logic_1164.all;

entity test_shift_reg is
    port (
        clk     : in  std_logic;
        din     : in  std_logic;
        dout    : out std_logic_vector(3 downto 0)
    );
end test_shift_reg;

architecture Behavioral of test_shift_reg is
    signal reg : std_logic_vector(3 downto 0) := "0000";
begin
    -- Output the register value
    dout <= reg;

    -- Shift register process
    process(clk)
    begin
        if rising_edge(clk) then
            -- Shift left and insert new bit at LSB
            reg(3) <= reg(2);
            reg(2) <= reg(1);
            reg(1) <= reg(0);
            reg(0) <= din;
        end if;
    end process;

end Behavioral;
