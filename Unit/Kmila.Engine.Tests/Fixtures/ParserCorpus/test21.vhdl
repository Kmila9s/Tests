-- Test 21: 16-bit Serial-In Serial-Out Shift Register (SISO)
-- Curriculum: Diseño de Sistemas Digitales, Unit 1.1.1
-- Exercises: clocked process, sync reset, downto-vector indexing.

library IEEE;
use IEEE.std_logic_1164.all;

entity ShiftRegSISO is
    generic (WIDTH : integer := 16);
    port (
        clk    : in  std_logic;
        rst_n  : in  std_logic;
        sh_en  : in  std_logic;
        d_in   : in  std_logic;
        d_out  : out std_logic
    );
end ShiftRegSISO;

architecture RTL of ShiftRegSISO is
    signal stage : std_logic_vector(WIDTH - 1 downto 0) := (others => '0');
begin
    shift_proc : process(clk)
    begin
        if rising_edge(clk) then
            if rst_n = '0' then
                stage <= (others => '0');
            elsif sh_en = '1' then
                stage <= stage(WIDTH - 2 downto 0) & d_in;
            end if;
        end if;
    end process shift_proc;

    d_out <= stage(WIDTH - 1);
end RTL;
