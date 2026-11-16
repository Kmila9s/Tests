-- Test 20: Full SoC-Style Design with All VHDL Constructs
-- Tests: Everything - entities, architectures, packages, generics, components, generates,
--        functions, procedures, records, arrays, attributes, configurations, all operators

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

-- =============================================================================
-- PACKAGE: SoC Types and Constants
-- =============================================================================
package SoC_Package is

    -- Clock and reset constants
    constant SYS_CLK_FREQ : integer := 100000000;
    constant UART_BAUD : integer := 115200;

    -- Bus widths
    constant ADDR_WIDTH : integer := 32;
    constant DATA_WIDTH : integer := 32;
    constant STRB_WIDTH : integer := DATA_WIDTH / 8;

    -- Memory map
    constant ROM_BASE : std_logic_vector(31 downto 0) := x"00000000";
    constant ROM_SIZE : integer := 16384;
    constant RAM_BASE : std_logic_vector(31 downto 0) := x"20000000";
    constant RAM_SIZE : integer := 65536;
    constant PERIPH_BASE : std_logic_vector(31 downto 0) := x"40000000";
    constant GPIO_OFFSET : std_logic_vector(31 downto 0) := x"00000000";
    constant UART_OFFSET : std_logic_vector(31 downto 0) := x"00001000";
    constant TIMER_OFFSET : std_logic_vector(31 downto 0) := x"00002000";
    constant SPI_OFFSET : std_logic_vector(31 downto 0) := x"00003000";

    -- Subtypes
    subtype addr_t is std_logic_vector(ADDR_WIDTH - 1 downto 0);
    subtype data_t is std_logic_vector(DATA_WIDTH - 1 downto 0);
    subtype strb_t is std_logic_vector(STRB_WIDTH - 1 downto 0);

    -- AXI-Lite bus record
    type axi_lite_m2s_t is record
        awaddr : addr_t;
        awvalid : std_logic;
        wdata : data_t;
        wstrb : strb_t;
        wvalid : std_logic;
        bready : std_logic;
        araddr : addr_t;
        arvalid : std_logic;
        rready : std_logic;
    end record axi_lite_m2s_t;

    type axi_lite_s2m_t is record
        awready : std_logic;
        wready : std_logic;
        bresp : std_logic_vector(1 downto 0);
        bvalid : std_logic;
        arready : std_logic;
        rdata : data_t;
        rresp : std_logic_vector(1 downto 0);
        rvalid : std_logic;
    end record axi_lite_s2m_t;

    -- Array of bus interfaces for multiple slaves
    type axi_lite_m2s_array_t is array (natural range <>) of axi_lite_m2s_t;
    type axi_lite_s2m_array_t is array (natural range <>) of axi_lite_s2m_t;

    -- Interrupt controller types
    type irq_vector_t is array (0 to 31) of std_logic;
    type irq_priority_t is array (0 to 31) of unsigned(3 downto 0);

    -- DMA descriptor
    type dma_descriptor_t is record
        src_addr : addr_t;
        dst_addr : addr_t;
        length : unsigned(15 downto 0);
        control : std_logic_vector(7 downto 0);
        status : std_logic_vector(7 downto 0);
        next_desc : addr_t;
    end record dma_descriptor_t;

    type dma_descriptor_array_t is array (natural range <>) of dma_descriptor_t;

    -- Constants for initialization
    constant AXI_LITE_M2S_INIT : axi_lite_m2s_t := (
        awaddr => (others => '0'),
        awvalid => '0',
        wdata => (others => '0'),
        wstrb => (others => '0'),
        wvalid => '0',
        bready => '0',
        araddr => (others => '0'),
        arvalid => '0',
        rready => '0'
    );

    constant AXI_LITE_S2M_INIT : axi_lite_s2m_t := (
        awready => '0',
        wready => '0',
        bresp => "00",
        bvalid => '0',
        arready => '0',
        rdata => (others => '0'),
        rresp => "00",
        rvalid => '0'
    );

    -- Helper functions
    function addr_decode(addr : addr_t; base : addr_t; size : integer) return boolean;
    function log2ceil(val : integer) return integer;
    function reverse_bits(vec : std_logic_vector) return std_logic_vector;

    -- Procedures
    procedure axi_write(
        signal m2s : out axi_lite_m2s_t;
        signal s2m : in axi_lite_s2m_t;
        addr : in addr_t;
        data : in data_t;
        signal done : out std_logic
    );

    procedure axi_read(
        signal m2s : out axi_lite_m2s_t;
        signal s2m : in axi_lite_s2m_t;
        addr : in addr_t;
        signal data : out data_t;
        signal done : out std_logic
    );

end SoC_Package;

package body SoC_Package is

    function addr_decode(addr : addr_t; base : addr_t; size : integer) return boolean is
        variable addr_u, base_u : unsigned(ADDR_WIDTH - 1 downto 0);
    begin
        addr_u := unsigned(addr);
        base_u := unsigned(base);
        return (addr_u >= base_u) and (addr_u < (base_u + size));
    end addr_decode;

    function log2ceil(val : integer) return integer is
        variable result : integer := 0;
        variable temp : integer := val - 1;
    begin
        while temp > 0 loop
            result := result + 1;
            temp := temp / 2;
        end loop;
        return result;
    end log2ceil;

    function reverse_bits(vec : std_logic_vector) return std_logic_vector is
        variable result : std_logic_vector(vec'range);
    begin
        for i in vec'range loop
            result(vec'high - i + vec'low) := vec(i);
        end loop;
        return result;
    end reverse_bits;

    procedure axi_write(
        signal m2s : out axi_lite_m2s_t;
        signal s2m : in axi_lite_s2m_t;
        addr : in addr_t;
        data : in data_t;
        signal done : out std_logic
    ) is
    begin
        m2s.awaddr <= addr;
        m2s.awvalid <= '1';
        m2s.wdata <= data;
        m2s.wstrb <= (others => '1');
        m2s.wvalid <= '1';
        m2s.bready <= '1';
        done <= '0';
        wait until s2m.bvalid = '1';
        m2s.awvalid <= '0';
        m2s.wvalid <= '0';
        done <= '1';
    end axi_write;

    procedure axi_read(
        signal m2s : out axi_lite_m2s_t;
        signal s2m : in axi_lite_s2m_t;
        addr : in addr_t;
        signal data : out data_t;
        signal done : out std_logic
    ) is
    begin
        m2s.araddr <= addr;
        m2s.arvalid <= '1';
        m2s.rready <= '1';
        done <= '0';
        wait until s2m.rvalid = '1';
        data <= s2m.rdata;
        m2s.arvalid <= '0';
        done <= '1';
    end axi_read;

end SoC_Package;

-- =============================================================================
-- ENTITY: GPIO Controller
-- =============================================================================
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.SoC_Package.all;

entity GPIO_Controller is
    generic (
        NUM_PINS : integer := 32;
        ACTIVE_HIGH_IRQ : boolean := true
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- AXI-Lite slave interface
        s_axi_m2s : in axi_lite_m2s_t;
        s_axi_s2m : out axi_lite_s2m_t;
        -- GPIO pins
        gpio_i : in std_logic_vector(NUM_PINS - 1 downto 0);
        gpio_o : out std_logic_vector(NUM_PINS - 1 downto 0);
        gpio_t : out std_logic_vector(NUM_PINS - 1 downto 0);
        -- Interrupt
        irq : out std_logic
    );
end GPIO_Controller;

architecture RTL of GPIO_Controller is
    -- Register offsets
    constant REG_DATA : integer := 0;
    constant REG_DIR : integer := 4;
    constant REG_IRQ_EN : integer := 8;
    constant REG_IRQ_STAT : integer := 12;
    constant REG_IRQ_EDGE : integer := 16;

    -- Registers
    signal data_reg : std_logic_vector(NUM_PINS - 1 downto 0) := (others => '0');
    signal dir_reg : std_logic_vector(NUM_PINS - 1 downto 0) := (others => '0');
    signal irq_en_reg : std_logic_vector(NUM_PINS - 1 downto 0) := (others => '0');
    signal irq_stat_reg : std_logic_vector(NUM_PINS - 1 downto 0) := (others => '0');
    signal irq_edge_reg : std_logic_vector(NUM_PINS - 1 downto 0) := (others => '0');

    -- Input synchronization
    signal gpio_sync : std_logic_vector(NUM_PINS - 1 downto 0);
    signal gpio_prev : std_logic_vector(NUM_PINS - 1 downto 0);

    -- State machine
    constant S_IDLE : std_logic_vector(1 downto 0) := "00";
    constant S_WRITE : std_logic_vector(1 downto 0) := "01";
    constant S_READ : std_logic_vector(1 downto 0) := "10";
    signal state : std_logic_vector(1 downto 0) := S_IDLE;

    signal addr_reg : unsigned(7 downto 0);
    signal rdata_reg : data_t;
begin
    -- Input synchronization
    sync_proc : process(clk, rst)
    begin
        if rst = '1' then
            gpio_sync <= (others => '0');
            gpio_prev <= (others => '0');
        elsif rising_edge(clk) then
            gpio_sync <= gpio_i;
            gpio_prev <= gpio_sync;
        end if;
    end process sync_proc;

    -- Edge detection and interrupt generation
    irq_proc : process(clk, rst)
        variable edge_detect : std_logic_vector(NUM_PINS - 1 downto 0);
    begin
        if rst = '1' then
            irq_stat_reg <= (others => '0');
            irq <= '0';
        elsif rising_edge(clk) then
            -- Rising edge detection
            for i in 0 to NUM_PINS - 1 loop
                if irq_edge_reg(i) = '0' then
                    edge_detect(i) := gpio_sync(i) and not gpio_prev(i);
                else
                    edge_detect(i) := not gpio_sync(i) and gpio_prev(i);
                end if;
            end loop;

            irq_stat_reg <= irq_stat_reg or edge_detect;

            if ACTIVE_HIGH_IRQ then
                irq <= '1' when (irq_stat_reg and irq_en_reg) /= (irq_en_reg'range => '0') else '0';
            else
                irq <= '0' when (irq_stat_reg and irq_en_reg) /= (irq_en_reg'range => '0') else '1';
            end if;
        end if;
    end process irq_proc;

    -- AXI interface
    axi_proc : process(clk, rst)
        variable reg_offset : integer;
    begin
        if rst = '1' then
            state <= S_IDLE;
            s_axi_s2m <= AXI_LITE_S2M_INIT;
            data_reg <= (others => '0');
            dir_reg <= (others => '0');
            irq_en_reg <= (others => '0');
            irq_edge_reg <= (others => '0');
        elsif rising_edge(clk) then
            s_axi_s2m.bvalid <= '0';
            s_axi_s2m.rvalid <= '0';

            case state is
                when S_IDLE =>
                    if s_axi_m2s.awvalid = '1' and s_axi_m2s.wvalid = '1' then
                        addr_reg <= unsigned(s_axi_m2s.awaddr(7 downto 0));
                        state <= S_WRITE;
                        s_axi_s2m.awready <= '1';
                        s_axi_s2m.wready <= '1';
                    elsif s_axi_m2s.arvalid = '1' then
                        addr_reg <= unsigned(s_axi_m2s.araddr(7 downto 0));
                        state <= S_READ;
                        s_axi_s2m.arready <= '1';
                    end if;

                when S_WRITE =>
                    s_axi_s2m.awready <= '0';
                    s_axi_s2m.wready <= '0';
                    reg_offset := to_integer(addr_reg);

                    case reg_offset is
                        when REG_DATA => data_reg <= s_axi_m2s.wdata(NUM_PINS - 1 downto 0);
                        when REG_DIR => dir_reg <= s_axi_m2s.wdata(NUM_PINS - 1 downto 0);
                        when REG_IRQ_EN => irq_en_reg <= s_axi_m2s.wdata(NUM_PINS - 1 downto 0);
                        when REG_IRQ_STAT => irq_stat_reg <= irq_stat_reg and not s_axi_m2s.wdata(NUM_PINS - 1 downto 0);
                        when REG_IRQ_EDGE => irq_edge_reg <= s_axi_m2s.wdata(NUM_PINS - 1 downto 0);
                        when others => null;
                    end case;

                    s_axi_s2m.bresp <= "00";
                    s_axi_s2m.bvalid <= '1';
                    state <= S_IDLE;

                when S_READ =>
                    s_axi_s2m.arready <= '0';
                    reg_offset := to_integer(addr_reg);

                    rdata_reg <= (others => '0');
                    case reg_offset is
                        when REG_DATA =>
                            for i in 0 to NUM_PINS - 1 loop
                                if dir_reg(i) = '0' then
                                    rdata_reg(i) <= gpio_sync(i);
                                else
                                    rdata_reg(i) <= data_reg(i);
                                end if;
                            end loop;
                        when REG_DIR => rdata_reg(NUM_PINS - 1 downto 0) <= dir_reg;
                        when REG_IRQ_EN => rdata_reg(NUM_PINS - 1 downto 0) <= irq_en_reg;
                        when REG_IRQ_STAT => rdata_reg(NUM_PINS - 1 downto 0) <= irq_stat_reg;
                        when REG_IRQ_EDGE => rdata_reg(NUM_PINS - 1 downto 0) <= irq_edge_reg;
                        when others => rdata_reg <= (others => '0');
                    end case;

                    s_axi_s2m.rdata <= rdata_reg;
                    s_axi_s2m.rresp <= "00";
                    s_axi_s2m.rvalid <= '1';
                    state <= S_IDLE;

                when others =>
                    state <= S_IDLE;
            end case;
        end if;
    end process axi_proc;

    -- Output assignments
    gpio_o <= data_reg;
    gpio_t <= dir_reg;

end RTL;

-- =============================================================================
-- ENTITY: UART Controller
-- =============================================================================
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.SoC_Package.all;

entity UART_Controller is
    generic (
        CLK_FREQ : integer := SYS_CLK_FREQ;
        BAUD_RATE : integer := UART_BAUD;
        FIFO_DEPTH : integer := 16
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        s_axi_m2s : in axi_lite_m2s_t;
        s_axi_s2m : out axi_lite_s2m_t;
        uart_tx : out std_logic;
        uart_rx : in std_logic;
        irq_tx : out std_logic;
        irq_rx : out std_logic
    );
end UART_Controller;

architecture RTL of UART_Controller is
    constant DIVISOR : integer := CLK_FREQ / BAUD_RATE;
    constant FIFO_ADDR_WIDTH : integer := log2ceil(FIFO_DEPTH);

    -- TX state machine
    constant TX_ST_IDLE : std_logic_vector(2 downto 0) := "000";
    constant TX_ST_START : std_logic_vector(2 downto 0) := "001";
    constant TX_ST_DATA : std_logic_vector(2 downto 0) := "010";
    constant TX_ST_STOP : std_logic_vector(2 downto 0) := "011";

    signal tx_state : std_logic_vector(2 downto 0) := TX_ST_IDLE;
    signal tx_data : std_logic_vector(7 downto 0);
    signal tx_bit_cnt : unsigned(2 downto 0);
    signal tx_clk_cnt : unsigned(15 downto 0);
    signal tx_busy : std_logic := '0';

    -- RX state machine
    constant RX_ST_IDLE : std_logic_vector(2 downto 0) := "000";
    constant RX_ST_START : std_logic_vector(2 downto 0) := "001";
    constant RX_ST_DATA : std_logic_vector(2 downto 0) := "010";
    constant RX_ST_STOP : std_logic_vector(2 downto 0) := "011";

    signal rx_state : std_logic_vector(2 downto 0) := RX_ST_IDLE;
    signal rx_data : std_logic_vector(7 downto 0);
    signal rx_bit_cnt : unsigned(2 downto 0);
    signal rx_clk_cnt : unsigned(15 downto 0);
    signal rx_sync : std_logic_vector(2 downto 0);

    -- FIFOs
    signal tx_fifo : std_logic_vector((FIFO_DEPTH * 8) - 1 downto 0) := (others => '0');
    signal tx_wr_ptr, tx_rd_ptr : unsigned(FIFO_ADDR_WIDTH - 1 downto 0) := (others => '0');
    signal tx_count : unsigned(FIFO_ADDR_WIDTH downto 0) := (others => '0');

    signal rx_fifo : std_logic_vector((FIFO_DEPTH * 8) - 1 downto 0) := (others => '0');
    signal rx_wr_ptr, rx_rd_ptr : unsigned(FIFO_ADDR_WIDTH - 1 downto 0) := (others => '0');
    signal rx_count : unsigned(FIFO_ADDR_WIDTH downto 0) := (others => '0');

begin
    -- RX synchronization
    rx_sync_proc : process(clk)
    begin
        if rising_edge(clk) then
            rx_sync <= rx_sync(1 downto 0) & uart_rx;
        end if;
    end process rx_sync_proc;

    -- TX state machine
    tx_fsm : process(clk, rst)
    begin
        if rst = '1' then
            tx_state <= TX_ST_IDLE;
            tx_bit_cnt <= (others => '0');
            tx_clk_cnt <= (others => '0');
            tx_busy <= '0';
            uart_tx <= '1';
        elsif rising_edge(clk) then
            case tx_state is
                when TX_ST_IDLE =>
                    uart_tx <= '1';
                    tx_busy <= '0';
                    if tx_count > 0 then
                        tx_data <= tx_fifo(to_integer(tx_rd_ptr) * 8 + 7 downto to_integer(tx_rd_ptr) * 8);
                        tx_rd_ptr <= tx_rd_ptr + 1;
                        tx_count <= tx_count - 1;
                        tx_state <= TX_ST_START;
                        tx_clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                        tx_busy <= '1';
                    end if;

                when TX_ST_START =>
                    uart_tx <= '0';
                    if tx_clk_cnt = 0 then
                        tx_clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                        tx_bit_cnt <= (others => '0');
                        tx_state <= TX_ST_DATA;
                    else
                        tx_clk_cnt <= tx_clk_cnt - 1;
                    end if;

                when TX_ST_DATA =>
                    uart_tx <= tx_data(to_integer(tx_bit_cnt));
                    if tx_clk_cnt = 0 then
                        tx_clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                        if tx_bit_cnt = 7 then
                            tx_state <= TX_ST_STOP;
                        else
                            tx_bit_cnt <= tx_bit_cnt + 1;
                        end if;
                    else
                        tx_clk_cnt <= tx_clk_cnt - 1;
                    end if;

                when TX_ST_STOP =>
                    uart_tx <= '1';
                    if tx_clk_cnt = 0 then
                        tx_state <= TX_ST_IDLE;
                    else
                        tx_clk_cnt <= tx_clk_cnt - 1;
                    end if;

                when others =>
                    tx_state <= TX_ST_IDLE;
            end case;
        end if;
    end process tx_fsm;

    -- RX state machine
    rx_fsm : process(clk, rst)
    begin
        if rst = '1' then
            rx_state <= RX_ST_IDLE;
            rx_bit_cnt <= (others => '0');
            rx_clk_cnt <= (others => '0');
            rx_data <= (others => '0');
        elsif rising_edge(clk) then
            case rx_state is
                when RX_ST_IDLE =>
                    if rx_sync(2) = '0' then
                        rx_state <= RX_ST_START;
                        rx_clk_cnt <= to_unsigned(DIVISOR / 2, 16);
                    end if;

                when RX_ST_START =>
                    if rx_clk_cnt = 0 then
                        if rx_sync(2) = '0' then
                            rx_clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                            rx_bit_cnt <= (others => '0');
                            rx_state <= RX_ST_DATA;
                        else
                            rx_state <= RX_ST_IDLE;
                        end if;
                    else
                        rx_clk_cnt <= rx_clk_cnt - 1;
                    end if;

                when RX_ST_DATA =>
                    if rx_clk_cnt = 0 then
                        rx_data(to_integer(rx_bit_cnt)) <= rx_sync(2);
                        rx_clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                        if rx_bit_cnt = 7 then
                            rx_state <= RX_ST_STOP;
                        else
                            rx_bit_cnt <= rx_bit_cnt + 1;
                        end if;
                    else
                        rx_clk_cnt <= rx_clk_cnt - 1;
                    end if;

                when RX_ST_STOP =>
                    if rx_clk_cnt = 0 then
                        if rx_sync(2) = '1' and rx_count < FIFO_DEPTH then
                            rx_fifo(to_integer(rx_wr_ptr) * 8 + 7 downto to_integer(rx_wr_ptr) * 8) <= rx_data;
                            rx_wr_ptr <= rx_wr_ptr + 1;
                            rx_count <= rx_count + 1;
                        end if;
                        rx_state <= RX_ST_IDLE;
                    else
                        rx_clk_cnt <= rx_clk_cnt - 1;
                    end if;

                when others =>
                    rx_state <= RX_ST_IDLE;
            end case;
        end if;
    end process rx_fsm;

    -- IRQ generation
    irq_tx <= '1' when tx_count < FIFO_DEPTH / 2 else '0';
    irq_rx <= '1' when rx_count > 0 else '0';

    -- AXI interface (simplified)
    s_axi_s2m <= AXI_LITE_S2M_INIT;

end RTL;

-- =============================================================================
-- TOP-LEVEL: SoC
-- =============================================================================
library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use work.SoC_Package.all;

entity MiniSoC is
    generic (
        NUM_GPIO : integer := 32;
        ENABLE_UART : boolean := true;
        ENABLE_DEBUG : boolean := false
    );
    port (
        -- System
        sys_clk : in std_logic;
        sys_rst_n : in std_logic;
        -- GPIO
        gpio_pins : inOut std_logic_vector(NUM_GPIO - 1 downto 0);
        -- UART
        uart_txd : out std_logic;
        uart_rxd : in std_logic;
        -- LEDs and buttons
        leds : out std_logic_vector(7 downto 0);
        buttons : in std_logic_vector(3 downto 0)
    );
end MiniSoC;

architecture Structural of MiniSoC is

    component GPIO_Controller is
        generic (
            NUM_PINS : integer := 32;
            ACTIVE_HIGH_IRQ : boolean := true
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            s_axi_m2s : in axi_lite_m2s_t;
            s_axi_s2m : out axi_lite_s2m_t;
            gpio_i : in std_logic_vector(NUM_PINS - 1 downto 0);
            gpio_o : out std_logic_vector(NUM_PINS - 1 downto 0);
            gpio_t : out std_logic_vector(NUM_PINS - 1 downto 0);
            irq : out std_logic
        );
    end component;

    component UART_Controller is
        generic (
            CLK_FREQ : integer := SYS_CLK_FREQ;
            BAUD_RATE : integer := UART_BAUD;
            FIFO_DEPTH : integer := 16
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            s_axi_m2s : in axi_lite_m2s_t;
            s_axi_s2m : out axi_lite_s2m_t;
            uart_tx : out std_logic;
            uart_rx : in std_logic;
            irq_tx : out std_logic;
            irq_rx : out std_logic
        );
    end component;

    signal rst : std_logic;
    signal gpio_m2s : axi_lite_m2s_t := AXI_LITE_M2S_INIT;
    signal gpio_s2m : axi_lite_s2m_t;
    signal uart_m2s : axi_lite_m2s_t := AXI_LITE_M2S_INIT;
    signal uart_s2m : axi_lite_s2m_t;

    signal gpio_o_int, gpio_t_int : std_logic_vector(NUM_GPIO - 1 downto 0);
    signal gpio_irq, uart_irq_tx, uart_irq_rx : std_logic;

begin

    rst <= not sys_rst_n;

    -- GPIO Controller
    gpio_ctrl : GPIO_Controller
        generic map (
            NUM_PINS => NUM_GPIO,
            ACTIVE_HIGH_IRQ => true
        )
        port map (
            clk => sys_clk,
            rst => rst,
            s_axi_m2s => gpio_m2s,
            s_axi_s2m => gpio_s2m,
            gpio_i => gpio_pins,
            gpio_o => gpio_o_int,
            gpio_t => gpio_t_int,
            irq => gpio_irq
        );

    -- GPIO tristate buffers
    gpio_gen : for i in 0 to NUM_GPIO - 1 generate
        gpio_pins(i) <= gpio_o_int(i) when gpio_t_int(i) = '1' else 'Z';
    end generate gpio_gen;

    -- UART Controller (conditional)
    gen_uart : if ENABLE_UART generate
        uart_ctrl : UART_Controller
            generic map (
                CLK_FREQ => SYS_CLK_FREQ,
                BAUD_RATE => UART_BAUD,
                FIFO_DEPTH => 16
            )
            port map (
                clk => sys_clk,
                rst => rst,
                s_axi_m2s => uart_m2s,
                s_axi_s2m => uart_s2m,
                uart_tx => uart_txd,
                uart_rx => uart_rxd,
                irq_tx => uart_irq_tx,
                irq_rx => uart_irq_rx
            );
    end generate gen_uart;

    gen_no_uart : if not ENABLE_UART generate
        uart_txd <= '1';
        uart_irq_tx <= '0';
        uart_irq_rx <= '0';
    end generate gen_no_uart;

    -- LED output from GPIO
    leds <= gpio_o_int(7 downto 0);

    -- Debug logic (conditional)
    gen_debug : if ENABLE_DEBUG generate
        signal debug_counter : unsigned(31 downto 0) := (others => '0');
    begin
        debug_proc : process(sys_clk, rst)
        begin
            if rst = '1' then
                debug_counter <= (others => '0');
            elsif rising_edge(sys_clk) then
                debug_counter <= debug_counter + 1;
            end if;
        end process debug_proc;
    end generate gen_debug;

end Structural;

-- Configuration
configuration MiniSoC_Full of MiniSoC is
    for Structural
        for gpio_ctrl : GPIO_Controller
            use entity work.GPIO_Controller(RTL);
        end for;
        for gen_uart
            for uart_ctrl : UART_Controller
                use entity work.UART_Controller(RTL);
            end for;
        end for;
    end for;
end MiniSoC_Full;
