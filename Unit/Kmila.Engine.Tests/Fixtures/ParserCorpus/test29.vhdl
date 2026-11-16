-- Test 29: 256x16 Dual-Port RAM (independent read/write)
-- Curriculum: Diseño de Sistemas Digitales, Unit 4.2

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity DualPortRAM is
    generic (
        ADDR_WIDTH : integer := 8;
        DATA_WIDTH : integer := 16
    );
    port (
        clk    : in  std_logic;
        wr_en  : in  std_logic;
        wr_addr: in  std_logic_vector(ADDR_WIDTH - 1 downto 0);
        wr_data: in  std_logic_vector(DATA_WIDTH - 1 downto 0);
        rd_addr: in  std_logic_vector(ADDR_WIDTH - 1 downto 0);
        rd_data: out std_logic_vector(DATA_WIDTH - 1 downto 0)
    );
end DualPortRAM;

architecture RTL of DualPortRAM is
    -- Flat-vector RAM (avoids array-of-vector type the parser may not synth).
    signal mem : std_logic_vector((2**ADDR_WIDTH * DATA_WIDTH) - 1 downto 0) := (others => '0');
begin
    write_proc : process(clk)
    begin
        if rising_edge(clk) then
            if wr_en = '1' then
                mem((to_integer(unsigned(wr_addr)) + 1) * DATA_WIDTH - 1
                    downto to_integer(unsigned(wr_addr)) * DATA_WIDTH) <= wr_data;
            end if;
        end if;
    end process write_proc;

    read_proc : process(clk)
    begin
        if rising_edge(clk) then
            rd_data <= mem((to_integer(unsigned(rd_addr)) + 1) * DATA_WIDTH - 1
                           downto to_integer(unsigned(rd_addr)) * DATA_WIDTH);
        end if;
    end process read_proc;
end RTL;
