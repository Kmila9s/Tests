-- Test 24: 8-bit Johnson Counter (twisted-ring)
-- Curriculum: Diseño de Sistemas Digitales, Unit 3.3

library IEEE;
use IEEE.std_logic_1164.all;

entity JohnsonCounter is
    generic (WIDTH : integer := 8);
    port (
        clk   : in  std_logic;
        rst_n : in  std_logic;
        en    : in  std_logic;
        q     : out std_logic_vector(WIDTH - 1 downto 0)
    );
end JohnsonCounter;

architecture RTL of JohnsonCounter is
    signal reg : std_logic_vector(WIDTH - 1 downto 0) := (others => '0');
begin
    cnt_proc : process(clk, rst_n)
    begin
        if rst_n = '0' then
            reg <= (others => '0');
        elsif rising_edge(clk) then
            if en = '1' then
                reg <= (not reg(0)) & reg(WIDTH - 1 downto 1);
            end if;
        end if;
    end process cnt_proc;

    q <= reg;
end RTL;
