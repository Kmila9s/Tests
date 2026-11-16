-- Test 34: SPI Master (CPOL=0, CPHA=0, 8-bit, single slave)
-- Curriculum: Sistemas en Chip, Unit 3.2

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity SPI_Master is
    generic (CLK_DIV : integer := 4);
    port (
        clk     : in  std_logic;
        rst     : in  std_logic;
        start   : in  std_logic;
        miso    : in  std_logic;
        tx_data : in  std_logic_vector(7 downto 0);
        rx_data : out std_logic_vector(7 downto 0);
        sclk    : out std_logic;
        mosi    : out std_logic;
        cs_n    : out std_logic;
        busy    : out std_logic
    );
end SPI_Master;

architecture RTL of SPI_Master is
    signal sclk_int : std_logic := '0';
    signal cs_int   : std_logic := '1';
    signal shift_tx : std_logic_vector(7 downto 0) := (others => '0');
    signal shift_rx : std_logic_vector(7 downto 0) := (others => '0');
    signal bit_cnt  : unsigned(3 downto 0) := (others => '0');
    signal div_cnt  : unsigned(7 downto 0) := (others => '0');
    signal active   : std_logic := '0';
begin
    spi_proc : process(clk, rst)
    begin
        if rst = '1' then
            sclk_int <= '0';
            cs_int   <= '1';
            shift_tx <= (others => '0');
            shift_rx <= (others => '0');
            bit_cnt  <= (others => '0');
            div_cnt  <= (others => '0');
            active   <= '0';
        elsif rising_edge(clk) then
            if active = '0' then
                if start = '1' then
                    shift_tx <= tx_data;
                    cs_int   <= '0';
                    bit_cnt  <= (others => '0');
                    div_cnt  <= to_unsigned(CLK_DIV - 1, 8);
                    active   <= '1';
                end if;
            else
                if div_cnt = 0 then
                    div_cnt  <= to_unsigned(CLK_DIV - 1, 8);
                    sclk_int <= not sclk_int;
                    if sclk_int = '0' then
                        shift_rx <= shift_rx(6 downto 0) & miso;
                    else
                        shift_tx <= shift_tx(6 downto 0) & '0';
                        if bit_cnt = 7 then
                            active <= '0';
                            cs_int <= '1';
                        else
                            bit_cnt <= bit_cnt + 1;
                        end if;
                    end if;
                else
                    div_cnt <= div_cnt - 1;
                end if;
            end if;
        end if;
    end process spi_proc;

    sclk    <= sclk_int;
    cs_n    <= cs_int;
    mosi    <= shift_tx(7);
    rx_data <= shift_rx;
    busy    <= active;
end RTL;
