-- Test 31: 256-entry Sine Lookup ROM (8-bit signed values)
-- Curriculum: Diseño de Sistemas Digitales, Unit 4.3 (ROM)

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity SineROM is
    port (
        clk  : in  std_logic;
        addr : in  std_logic_vector(7 downto 0);
        data : out std_logic_vector(7 downto 0)
    );
end SineROM;

architecture RTL of SineROM is
    -- Quarter-wave constants (a real design unrolls one quadrant).
    constant SIN_QUARTER : std_logic_vector(63 downto 0) :=
        x"0008101820283038";
    signal index : integer range 0 to 7;
begin
    addr_proc : process(clk)
    begin
        if rising_edge(clk) then
            index <= to_integer(unsigned(addr(2 downto 0)));
        end if;
    end process addr_proc;

    sample_proc : process(clk)
    begin
        if rising_edge(clk) then
            data <= SIN_QUARTER((index + 1) * 8 - 1 downto index * 8);
        end if;
    end process sample_proc;
end RTL;
