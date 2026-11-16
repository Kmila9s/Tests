-- Test 22: 32-bit Parallel-In Parallel-Out Register with Load Enable
-- Curriculum: Diseño de Sistemas Digitales, Unit 1.1.4 + 1.2

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity PIPO_Reg is
    generic (WIDTH : integer := 32);
    port (
        clk    : in  std_logic;
        rst    : in  std_logic;
        load   : in  std_logic;
        clr    : in  std_logic;
        d_in   : in  std_logic_vector(WIDTH - 1 downto 0);
        d_out  : out std_logic_vector(WIDTH - 1 downto 0)
    );
end PIPO_Reg;

architecture RTL of PIPO_Reg is
    signal q : std_logic_vector(WIDTH - 1 downto 0) := (others => '0');
begin
    reg_proc : process(clk, rst)
    begin
        if rst = '1' then
            q <= (others => '0');
        elsif rising_edge(clk) then
            if clr = '1' then
                q <= (others => '0');
            elsif load = '1' then
                q <= d_in;
            end if;
        end if;
    end process reg_proc;

    d_out <= q;
end RTL;
