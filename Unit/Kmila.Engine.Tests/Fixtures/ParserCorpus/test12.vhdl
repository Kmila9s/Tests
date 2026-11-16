-- Test 12: Complex State Machine with Multiple States, Nested Case, and Concurrent FSMs
-- Tests: Enumerated types, complex case statements, multiple processes, state encoding

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity SDRAMController is
    generic (
        ROW_BITS : integer := 13;
        COL_BITS : integer := 10;
        BANK_BITS : integer := 2;
        DATA_WIDTH : integer := 16;
        CAS_LATENCY : integer := 3;
        tRCD : integer := 3;
        tRP : integer := 3;
        tRFC : integer := 10;
        tMRD : integer := 2;
        REFRESH_CYCLES : integer := 4096;
        REFRESH_PERIOD : integer := 64000000
    );
    port (
        clk : in std_logic;
        rst_n : in std_logic;
        -- User interface
        req : in std_logic;
        req_ack : out std_logic;
        we : in std_logic;
        addr : in std_logic_vector(ROW_BITS + COL_BITS + BANK_BITS - 1 downto 0);
        wdata : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        rdata : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        rdata_valid : out std_logic;
        busy : out std_logic;
        -- SDRAM interface
        sdram_clk : out std_logic;
        sdram_cke : out std_logic;
        sdram_cs_n : out std_logic;
        sdram_ras_n : out std_logic;
        sdram_cas_n : out std_logic;
        sdram_we_n : out std_logic;
        sdram_ba : out std_logic_vector(BANK_BITS - 1 downto 0);
        sdram_addr : out std_logic_vector(ROW_BITS - 1 downto 0);
        sdram_dqm : out std_logic_vector((DATA_WIDTH / 8) - 1 downto 0);
        sdram_dq : inout std_logic_vector(DATA_WIDTH - 1 downto 0)
    );
end SDRAMController;

architecture RTL of SDRAMController is

    -- Main controller states
    constant STATE_INIT_WAIT : std_logic_vector(4 downto 0) := "00000";
    constant STATE_INIT_PRECHARGE : std_logic_vector(4 downto 0) := "00001";
    constant STATE_INIT_REFRESH1 : std_logic_vector(4 downto 0) := "00010";
    constant STATE_INIT_REFRESH2 : std_logic_vector(4 downto 0) := "00011";
    constant STATE_INIT_MODE_REG : std_logic_vector(4 downto 0) := "00100";
    constant STATE_IDLE : std_logic_vector(4 downto 0) := "00101";
    constant STATE_ACTIVATE : std_logic_vector(4 downto 0) := "00110";
    constant STATE_READ : std_logic_vector(4 downto 0) := "00111";
    constant STATE_READ_WAIT : std_logic_vector(4 downto 0) := "01000";
    constant STATE_WRITE : std_logic_vector(4 downto 0) := "01001";
    constant STATE_WRITE_WAIT : std_logic_vector(4 downto 0) := "01010";
    constant STATE_PRECHARGE : std_logic_vector(4 downto 0) := "01011";
    constant STATE_REFRESH : std_logic_vector(4 downto 0) := "01100";
    constant STATE_REFRESH_WAIT : std_logic_vector(4 downto 0) := "01101";

    signal state, next_state : std_logic_vector(4 downto 0) := STATE_INIT_WAIT;
    signal timer : unsigned(15 downto 0) := (others => '0');
    signal timer_load : std_logic := '0';
    signal timer_value : unsigned(15 downto 0) := (others => '0');
    signal timer_done : std_logic := '0';

    -- Refresh counter
    signal refresh_counter : unsigned(15 downto 0) := (others => '0');
    signal refresh_needed : std_logic := '0';
    signal refresh_ack : std_logic := '0';

    -- Address decomposition
    signal row_addr : std_logic_vector(ROW_BITS - 1 downto 0);
    signal col_addr : std_logic_vector(COL_BITS - 1 downto 0);
    signal bank_addr : std_logic_vector(BANK_BITS - 1 downto 0);

    -- Bank tracking (open row per bank)
    signal bank_active : std_logic_vector((2 ** BANK_BITS) - 1 downto 0) := (others => '0');
    signal bank_row : std_logic_vector((ROW_BITS * (2 ** BANK_BITS)) - 1 downto 0) := (others => '0');

    -- Latched request
    signal req_latched : std_logic := '0';
    signal we_latched : std_logic := '0';
    signal addr_latched : std_logic_vector(ROW_BITS + COL_BITS + BANK_BITS - 1 downto 0) := (others => '0');
    signal wdata_latched : std_logic_vector(DATA_WIDTH - 1 downto 0) := (others => '0');

    -- CAS latency pipeline
    signal cas_pipeline : std_logic_vector(CAS_LATENCY - 1 downto 0) := (others => '0');

    -- Commands
    constant CMD_NOP : std_logic_vector(3 downto 0) := "0111";
    constant CMD_ACTIVE : std_logic_vector(3 downto 0) := "0011";
    constant CMD_READ : std_logic_vector(3 downto 0) := "0101";
    constant CMD_WRITE : std_logic_vector(3 downto 0) := "0100";
    constant CMD_PRECHARGE : std_logic_vector(3 downto 0) := "0010";
    constant CMD_REFRESH : std_logic_vector(3 downto 0) := "0001";
    constant CMD_MODE_REG : std_logic_vector(3 downto 0) := "0000";

    signal cmd : std_logic_vector(3 downto 0) := CMD_NOP;
    signal dq_out_en : std_logic := '0';
    signal dq_out : std_logic_vector(DATA_WIDTH - 1 downto 0) := (others => '0');

begin

    -- Address decomposition
    bank_addr <= addr_latched(ROW_BITS + COL_BITS + BANK_BITS - 1 downto ROW_BITS + COL_BITS);
    row_addr <= addr_latched(ROW_BITS + COL_BITS - 1 downto COL_BITS);
    col_addr <= addr_latched(COL_BITS - 1 downto 0);

    -- Timer process
    timer_proc : process(clk, rst_n)
    begin
        if rst_n = '0' then
            timer <= (others => '0');
            timer_done <= '0';
        elsif rising_edge(clk) then
            if timer_load = '1' then
                timer <= timer_value;
                timer_done <= '0';
            elsif timer > 0 then
                timer <= timer - 1;
                timer_done <= '0';
            else
                timer_done <= '1';
            end if;
        end if;
    end process timer_proc;

    -- Refresh counter process
    refresh_proc : process(clk, rst_n)
    begin
        if rst_n = '0' then
            refresh_counter <= (others => '0');
            refresh_needed <= '0';
        elsif rising_edge(clk) then
            if refresh_ack = '1' then
                refresh_needed <= '0';
                refresh_counter <= (others => '0');
            elsif refresh_counter >= to_unsigned(REFRESH_PERIOD / REFRESH_CYCLES, 16) then
                refresh_needed <= '1';
            else
                refresh_counter <= refresh_counter + 1;
            end if;
        end if;
    end process refresh_proc;

    -- Main FSM combinational process
    fsm_comb : process(state, timer_done, req, req_latched, we_latched, refresh_needed,
                       bank_active, bank_row, row_addr, bank_addr)
        variable current_bank : integer;
        variable row_match : std_logic;
    begin
        next_state <= state;
        timer_load <= '0';
        timer_value <= (others => '0');
        cmd <= CMD_NOP;
        req_ack <= '0';
        refresh_ack <= '0';
        busy <= '1';
        dq_out_en <= '0';

        current_bank := to_integer(unsigned(bank_addr));

        -- Check if row is already active in the target bank
        if bank_active(current_bank) = '1' then
            if bank_row((current_bank + 1) * ROW_BITS - 1 downto current_bank * ROW_BITS) = row_addr then
                row_match := '1';
            else
                row_match := '0';
            end if;
        else
            row_match := '0';
        end if;

        case state is
            when STATE_INIT_WAIT =>
                if timer_done = '1' then
                    next_state <= STATE_INIT_PRECHARGE;
                    cmd <= CMD_PRECHARGE;
                    timer_load <= '1';
                    timer_value <= to_unsigned(tRP, 16);
                end if;

            when STATE_INIT_PRECHARGE =>
                if timer_done = '1' then
                    next_state <= STATE_INIT_REFRESH1;
                    cmd <= CMD_REFRESH;
                    timer_load <= '1';
                    timer_value <= to_unsigned(tRFC, 16);
                end if;

            when STATE_INIT_REFRESH1 =>
                if timer_done = '1' then
                    next_state <= STATE_INIT_REFRESH2;
                    cmd <= CMD_REFRESH;
                    timer_load <= '1';
                    timer_value <= to_unsigned(tRFC, 16);
                end if;

            when STATE_INIT_REFRESH2 =>
                if timer_done = '1' then
                    next_state <= STATE_INIT_MODE_REG;
                    cmd <= CMD_MODE_REG;
                    timer_load <= '1';
                    timer_value <= to_unsigned(tMRD, 16);
                end if;

            when STATE_INIT_MODE_REG =>
                if timer_done = '1' then
                    next_state <= STATE_IDLE;
                end if;

            when STATE_IDLE =>
                busy <= '0';
                if refresh_needed = '1' then
                    next_state <= STATE_REFRESH;
                    cmd <= CMD_REFRESH;
                    timer_load <= '1';
                    timer_value <= to_unsigned(tRFC, 16);
                    refresh_ack <= '1';
                elsif req = '1' or req_latched = '1' then
                    if bank_active(current_bank) = '1' and row_match = '0' then
                        -- Need to precharge first
                        next_state <= STATE_PRECHARGE;
                        cmd <= CMD_PRECHARGE;
                        timer_load <= '1';
                        timer_value <= to_unsigned(tRP, 16);
                    elsif bank_active(current_bank) = '0' then
                        -- Need to activate row
                        next_state <= STATE_ACTIVATE;
                        cmd <= CMD_ACTIVE;
                        timer_load <= '1';
                        timer_value <= to_unsigned(tRCD, 16);
                    else
                        -- Row already active, proceed to read/write
                        if we_latched = '1' then
                            next_state <= STATE_WRITE;
                            cmd <= CMD_WRITE;
                        else
                            next_state <= STATE_READ;
                            cmd <= CMD_READ;
                        end if;
                    end if;
                    req_ack <= '1';
                end if;

            when STATE_ACTIVATE =>
                if timer_done = '1' then
                    if we_latched = '1' then
                        next_state <= STATE_WRITE;
                        cmd <= CMD_WRITE;
                    else
                        next_state <= STATE_READ;
                        cmd <= CMD_READ;
                    end if;
                end if;

            when STATE_READ =>
                next_state <= STATE_READ_WAIT;
                timer_load <= '1';
                timer_value <= to_unsigned(CAS_LATENCY, 16);

            when STATE_READ_WAIT =>
                if timer_done = '1' then
                    next_state <= STATE_IDLE;
                end if;

            when STATE_WRITE =>
                dq_out_en <= '1';
                next_state <= STATE_WRITE_WAIT;
                timer_load <= '1';
                timer_value <= to_unsigned(2, 16);

            when STATE_WRITE_WAIT =>
                if timer_done = '1' then
                    next_state <= STATE_IDLE;
                end if;

            when STATE_PRECHARGE =>
                if timer_done = '1' then
                    next_state <= STATE_ACTIVATE;
                    cmd <= CMD_ACTIVE;
                    timer_load <= '1';
                    timer_value <= to_unsigned(tRCD, 16);
                end if;

            when STATE_REFRESH =>
                next_state <= STATE_REFRESH_WAIT;

            when STATE_REFRESH_WAIT =>
                if timer_done = '1' then
                    next_state <= STATE_IDLE;
                end if;

            when others =>
                next_state <= STATE_IDLE;
        end case;
    end process fsm_comb;

    -- FSM sequential process
    fsm_seq : process(clk, rst_n)
    begin
        if rst_n = '0' then
            state <= STATE_INIT_WAIT;
            req_latched <= '0';
            we_latched <= '0';
            addr_latched <= (others => '0');
            wdata_latched <= (others => '0');
            bank_active <= (others => '0');
            bank_row <= (others => '0');
            cas_pipeline <= (others => '0');
        elsif rising_edge(clk) then
            state <= next_state;

            -- Latch request
            if req = '1' and state = STATE_IDLE then
                req_latched <= '1';
                we_latched <= we;
                addr_latched <= addr;
                wdata_latched <= wdata;
            elsif state = STATE_READ or state = STATE_WRITE then
                req_latched <= '0';
            end if;

            -- Track bank state
            if state = STATE_ACTIVATE then
                bank_active(to_integer(unsigned(bank_addr))) <= '1';
                bank_row((to_integer(unsigned(bank_addr)) + 1) * ROW_BITS - 1 downto
                         to_integer(unsigned(bank_addr)) * ROW_BITS) <= row_addr;
            elsif state = STATE_PRECHARGE then
                bank_active(to_integer(unsigned(bank_addr))) <= '0';
            elsif state = STATE_REFRESH then
                bank_active <= (others => '0');
            end if;

            -- CAS latency pipeline
            cas_pipeline <= cas_pipeline(CAS_LATENCY - 2 downto 0) & '0';
            if state = STATE_READ then
                cas_pipeline(0) <= '1';
            end if;
        end if;
    end process fsm_seq;

    -- Output assignments
    sdram_clk <= clk;
    sdram_cke <= '1';
    sdram_cs_n <= cmd(3);
    sdram_ras_n <= cmd(2);
    sdram_cas_n <= cmd(1);
    sdram_we_n <= cmd(0);
    sdram_ba <= bank_addr;
    sdram_addr <= row_addr when state = STATE_ACTIVATE else
                  ("000" & col_addr) when state = STATE_READ or state = STATE_WRITE else
                  (others => '1');
    sdram_dqm <= (others => '0');
    sdram_dq <= wdata_latched when dq_out_en = '1' else (others => 'Z');
    dq_out <= wdata_latched;

    rdata <= sdram_dq;
    rdata_valid <= cas_pipeline(CAS_LATENCY - 1);

end RTL;
