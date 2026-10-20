-- Kmila example: 8-bit shift register driving 8 individual LEDs.
-- Showcases the bit-slice wiring UI — each LED widget is wired to a
-- single bit of `reg_out` via the `reg_out[N]` binding syntax in the
-- companion .kviz. SI (serial-in) switch controls what gets shifted
-- in; SHIFT switch enables the clock-edge advance. Watch the bit
-- pattern walk left across the LED row.
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity shift_register is
  port (
    clk      : in  STD_LOGIC;
    reset    : in  STD_LOGIC;
    shift_en : in  STD_LOGIC;
    si       : in  STD_LOGIC;
    reg_out  : out STD_LOGIC_VECTOR(7 downto 0)
  );
end entity shift_register;

architecture behavioral of shift_register is
  signal reg : STD_LOGIC_VECTOR(7 downto 0) := (others => '0');
begin
  process(clk, reset)
  begin
    if reset = '1' then
      reg <= (others => '0');
    elsif rising_edge(clk) then
      if shift_en = '1' then
        -- Shift left, SI enters at the LSB.
        reg <= reg(6 downto 0) & si;
      end if;
    end if;
  end process;

  reg_out <= reg;
end architecture behavioral;
