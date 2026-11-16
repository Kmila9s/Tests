-- Test 33: UART Transmitter FSM (8N1, configurable divisor)
-- Curriculum: Sistemas en Chip, Unit 3.1

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity UART_TX is
    generic (DIVISOR : integer := 1085);
    port (
        clk     : in  std_logic;
        rst     : in  std_logic;
        tx_start: in  std_logic;
        tx_data : in  std_logic_vector(7 downto 0);
        tx_busy : out std_logic;
        tx_line : out std_logic
    );
end UART_TX;

architecture RTL of UART_TX is
    constant ST_IDLE  : std_logic_vector(2 downto 0) := "000";
    constant ST_START : std_logic_vector(2 downto 0) := "001";
    constant ST_DATA  : std_logic_vector(2 downto 0) := "010";
    constant ST_STOP  : std_logic_vector(2 downto 0) := "011";

    signal state    : std_logic_vector(2 downto 0) := ST_IDLE;
    signal shifter  : std_logic_vector(7 downto 0) := (others => '0');
    signal bit_cnt  : unsigned(2 downto 0) := (others => '0');
    signal clk_cnt  : unsigned(15 downto 0) := (others => '0');
begin
    tx_fsm : process(clk, rst)
    begin
        if rst = '1' then
            state   <= ST_IDLE;
            tx_line <= '1';
            tx_busy <= '0';
            clk_cnt <= (others => '0');
            bit_cnt <= (others => '0');
        elsif rising_edge(clk) then
            case state is
                when ST_IDLE =>
                    tx_line <= '1';
                    tx_busy <= '0';
                    if tx_start = '1' then
                        shifter <= tx_data;
                        state   <= ST_START;
                        tx_busy <= '1';
                        clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                    end if;
                when ST_START =>
                    tx_line <= '0';
                    if clk_cnt = 0 then
                        state   <= ST_DATA;
                        bit_cnt <= (others => '0');
                        clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                    else
                        clk_cnt <= clk_cnt - 1;
                    end if;
                when ST_DATA =>
                    tx_line <= shifter(0);
                    if clk_cnt = 0 then
                        shifter <= '0' & shifter(7 downto 1);
                        clk_cnt <= to_unsigned(DIVISOR - 1, 16);
                        if bit_cnt = 7 then
                            state <= ST_STOP;
                        else
                            bit_cnt <= bit_cnt + 1;
                        end if;
                    else
                        clk_cnt <= clk_cnt - 1;
                    end if;
                when ST_STOP =>
                    tx_line <= '1';
                    if clk_cnt = 0 then
                        state <= ST_IDLE;
                    else
                        clk_cnt <= clk_cnt - 1;
                    end if;
                when others =>
                    state <= ST_IDLE;
            end case;
        end if;
    end process tx_fsm;
end RTL;
