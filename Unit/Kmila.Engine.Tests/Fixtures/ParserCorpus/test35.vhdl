-- Test 35: I2C-style slave responder (skeleton, ack-only)
-- Curriculum: Sistemas en Chip, Unit 3.3

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity I2C_Slave is
    -- 7-bit slave address as a constant (the parser doesn't accept vector
    -- widths on generic types, so we keep the address fixed in the body).
    port (
        clk        : in  std_logic;
        rst        : in  std_logic;
        sda_in     : in  std_logic;
        scl_in     : in  std_logic;
        sda_out    : out std_logic;
        sda_oe     : out std_logic;
        rx_data    : out std_logic_vector(7 downto 0);
        rx_valid   : out std_logic
    );
end I2C_Slave;

architecture RTL of I2C_Slave is
    constant SLAVE_ADDR : std_logic_vector(6 downto 0) := "1010000";
    constant ST_IDLE    : std_logic_vector(2 downto 0) := "000";
    constant ST_ADDR  : std_logic_vector(2 downto 0) := "001";
    constant ST_ACK1  : std_logic_vector(2 downto 0) := "010";
    constant ST_DATA  : std_logic_vector(2 downto 0) := "011";
    constant ST_ACK2  : std_logic_vector(2 downto 0) := "100";

    signal state   : std_logic_vector(2 downto 0) := ST_IDLE;
    signal shift   : std_logic_vector(7 downto 0) := (others => '0');
    signal bit_cnt : unsigned(3 downto 0) := (others => '0');
    signal addr_ok : std_logic := '0';
begin
    slave_proc : process(clk, rst)
    begin
        if rst = '1' then
            state    <= ST_IDLE;
            shift    <= (others => '0');
            bit_cnt  <= (others => '0');
            sda_oe   <= '0';
            sda_out  <= '1';
            rx_valid <= '0';
            addr_ok  <= '0';
        elsif rising_edge(clk) then
            rx_valid <= '0';
            sda_oe   <= '0';
            case state is
                when ST_IDLE =>
                    if scl_in = '1' and sda_in = '0' then
                        state   <= ST_ADDR;
                        bit_cnt <= (others => '0');
                    end if;
                when ST_ADDR =>
                    if scl_in = '1' then
                        shift <= shift(6 downto 0) & sda_in;
                        if bit_cnt = 7 then
                            state <= ST_ACK1;
                            if shift(7 downto 1) = SLAVE_ADDR then
                                addr_ok <= '1';
                            end if;
                        else
                            bit_cnt <= bit_cnt + 1;
                        end if;
                    end if;
                when ST_ACK1 =>
                    sda_oe  <= addr_ok;
                    sda_out <= '0';
                    state   <= ST_DATA;
                    bit_cnt <= (others => '0');
                when ST_DATA =>
                    if scl_in = '1' then
                        shift <= shift(6 downto 0) & sda_in;
                        if bit_cnt = 7 then
                            state <= ST_ACK2;
                        else
                            bit_cnt <= bit_cnt + 1;
                        end if;
                    end if;
                when ST_ACK2 =>
                    rx_data  <= shift;
                    rx_valid <= '1';
                    state    <= ST_IDLE;
                when others =>
                    state <= ST_IDLE;
            end case;
        end if;
    end process slave_proc;
end RTL;
