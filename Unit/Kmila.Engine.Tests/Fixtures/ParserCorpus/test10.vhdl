-- Test 10: Complex Generic Entities with Multiple Constraints and Attributes
-- Tests: Multiple generics, complex port expressions, attribute declarations, disconnection specs

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity GenericFIFO is
    generic (
        DATA_WIDTH : integer := 32;
        DEPTH : integer := 16;
        ALMOST_FULL_THRESHOLD : integer := 12;
        ALMOST_EMPTY_THRESHOLD : integer := 4;
        FIRST_WORD_FALL_THROUGH : boolean := true;
        SYNC_STAGES : integer := 2
    );
    port (
        -- Write domain
        wr_clk : in std_logic;
        wr_rst_n : in std_logic;
        wr_en : in std_logic;
        wr_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        full : out std_logic;
        almost_full : out std_logic;
        wr_count : out std_logic_vector(clog2(DEPTH) downto 0);
        -- Read domain
        rd_clk : in std_logic;
        rd_rst_n : in std_logic;
        rd_en : in std_logic;
        rd_data : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        empty : out std_logic;
        almost_empty : out std_logic;
        rd_count : out std_logic_vector(clog2(DEPTH) downto 0);
        -- Status
        overflow : out std_logic;
        underflow : out std_logic
    );
end GenericFIFO;

architecture RTL of GenericFIFO is
    -- Custom attributes
    attribute ram_style : string;
    attribute shreg_extract : string;
    attribute async_reg : string;

    -- Memory array type
    signal mem : std_logic_vector((DEPTH * DATA_WIDTH) - 1 downto 0) := (others => '0');
    attribute ram_style of mem : signal is "block";

    -- Write pointer (gray coded)
    signal wr_ptr_bin : unsigned(clog2(DEPTH) downto 0) := (others => '0');
    signal wr_ptr_gray : std_logic_vector(clog2(DEPTH) downto 0) := (others => '0');
    signal wr_ptr_gray_sync : std_logic_vector(clog2(DEPTH) downto 0) := (others => '0');
    attribute async_reg of wr_ptr_gray_sync : signal is "true";

    -- Read pointer (gray coded)
    signal rd_ptr_bin : unsigned(clog2(DEPTH) downto 0) := (others => '0');
    signal rd_ptr_gray : std_logic_vector(clog2(DEPTH) downto 0) := (others => '0');
    signal rd_ptr_gray_sync : std_logic_vector(clog2(DEPTH) downto 0) := (others => '0');
    attribute async_reg of rd_ptr_gray_sync : signal is "true";

    -- Internal flags
    signal full_i, empty_i : std_logic := '0';
    signal almost_full_i, almost_empty_i : std_logic := '0';

    -- Disconnection specification for guarded signals
    disconnect all : std_logic after 10 ns;

    -- Helper functions
    function clog2(val : integer) return integer is
        variable result : integer := 0;
        variable temp : integer := val - 1;
    begin
        while temp > 0 loop
            result := result + 1;
            temp := temp / 2;
        end loop;
        return result;
    end clog2;

    function bin_to_gray(bin : unsigned) return std_logic_vector is
        variable gray : std_logic_vector(bin'range);
    begin
        gray := std_logic_vector(bin xor ('0' & bin(bin'left downto 1)));
        return gray;
    end bin_to_gray;

    function gray_to_bin(gray : std_logic_vector) return unsigned is
        variable bin : unsigned(gray'range);
    begin
        bin(bin'left) := gray(gray'left);
        for i in gray'left - 1 downto 0 loop
            bin(i) := bin(i + 1) xor gray(i);
        end loop;
        return bin;
    end gray_to_bin;

begin
    -- Write process
    write_proc : process(wr_clk, wr_rst_n)
        variable next_wr_ptr : unsigned(clog2(DEPTH) downto 0);
    begin
        if wr_rst_n = '0' then
            wr_ptr_bin <= (others => '0');
            wr_ptr_gray <= (others => '0');
            overflow <= '0';
        elsif rising_edge(wr_clk) then
            overflow <= '0';
            if wr_en = '1' then
                if full_i = '0' then
                    next_wr_ptr := wr_ptr_bin + 1;
                    wr_ptr_bin <= next_wr_ptr;
                    wr_ptr_gray <= bin_to_gray(next_wr_ptr);
                else
                    overflow <= '1';
                end if;
            end if;
        end if;
    end process write_proc;

    -- Read process
    read_proc : process(rd_clk, rd_rst_n)
        variable next_rd_ptr : unsigned(clog2(DEPTH) downto 0);
    begin
        if rd_rst_n = '0' then
            rd_ptr_bin <= (others => '0');
            rd_ptr_gray <= (others => '0');
            underflow <= '0';
        elsif rising_edge(rd_clk) then
            underflow <= '0';
            if rd_en = '1' then
                if empty_i = '0' then
                    next_rd_ptr := rd_ptr_bin + 1;
                    rd_ptr_bin <= next_rd_ptr;
                    rd_ptr_gray <= bin_to_gray(next_rd_ptr);
                else
                    underflow <= '1';
                end if;
            end if;
        end if;
    end process read_proc;

    -- Synchronizers for CDC
    sync_wr_to_rd : process(rd_clk, rd_rst_n)
    begin
        if rd_rst_n = '0' then
            wr_ptr_gray_sync <= (others => '0');
        elsif rising_edge(rd_clk) then
            wr_ptr_gray_sync <= wr_ptr_gray;
        end if;
    end process sync_wr_to_rd;

    sync_rd_to_wr : process(wr_clk, wr_rst_n)
    begin
        if wr_rst_n = '0' then
            rd_ptr_gray_sync <= (others => '0');
        elsif rising_edge(wr_clk) then
            rd_ptr_gray_sync <= rd_ptr_gray;
        end if;
    end process sync_rd_to_wr;

    -- Flag generation
    full_i <= '1' when wr_ptr_gray = (not rd_ptr_gray_sync(clog2(DEPTH) downto clog2(DEPTH) - 1) & rd_ptr_gray_sync(clog2(DEPTH) - 2 downto 0)) else '0';
    empty_i <= '1' when rd_ptr_gray = wr_ptr_gray_sync else '0';

    full <= full_i;
    empty <= empty_i;
    almost_full <= almost_full_i;
    almost_empty <= almost_empty_i;

end RTL;

configuration GenericFIFO_Config of GenericFIFO is
    for RTL
    end for;
end GenericFIFO_Config;
