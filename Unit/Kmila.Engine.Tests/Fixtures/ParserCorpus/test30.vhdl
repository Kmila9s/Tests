-- Test 30: Synchronous FIFO (32 deep, 8 wide) with full / empty / half flags
-- Curriculum: Diseño de Sistemas Digitales, Unit 4.4

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity SyncFIFO is
    generic (
        DEPTH : integer := 32;
        WIDTH : integer := 8
    );
    port (
        clk    : in  std_logic;
        rst    : in  std_logic;
        wr_en  : in  std_logic;
        rd_en  : in  std_logic;
        d_in   : in  std_logic_vector(WIDTH - 1 downto 0);
        d_out  : out std_logic_vector(WIDTH - 1 downto 0);
        full   : out std_logic;
        empty  : out std_logic;
        half   : out std_logic
    );
end SyncFIFO;

architecture RTL of SyncFIFO is
    signal mem    : std_logic_vector(DEPTH * WIDTH - 1 downto 0) := (others => '0');
    signal wr_ptr : unsigned(4 downto 0) := (others => '0');
    signal rd_ptr : unsigned(4 downto 0) := (others => '0');
    signal count  : unsigned(5 downto 0) := (others => '0');
begin
    fifo_proc : process(clk, rst)
    begin
        if rst = '1' then
            wr_ptr <= (others => '0');
            rd_ptr <= (others => '0');
            count  <= (others => '0');
        elsif rising_edge(clk) then
            if wr_en = '1' and count < DEPTH then
                mem((to_integer(wr_ptr) + 1) * WIDTH - 1
                    downto to_integer(wr_ptr) * WIDTH) <= d_in;
                wr_ptr <= wr_ptr + 1;
                count  <= count + 1;
            end if;
            if rd_en = '1' and count > 0 then
                d_out <= mem((to_integer(rd_ptr) + 1) * WIDTH - 1
                             downto to_integer(rd_ptr) * WIDTH);
                rd_ptr <= rd_ptr + 1;
                count  <= count - 1;
            end if;
        end if;
    end process fifo_proc;

    full  <= '1' when count = DEPTH else '0';
    empty <= '1' when count = 0 else '0';
    half  <= '1' when count >= DEPTH / 2 else '0';
end RTL;
