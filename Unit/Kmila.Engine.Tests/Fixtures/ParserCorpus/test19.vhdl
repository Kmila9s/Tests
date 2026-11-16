-- Test 19: Complex Conditional Generates with Nested Logic
-- Tests: if-generate, for-generate, case-generate (VHDL-2008), nested generates, generate with attributes

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;
use IEEE.math_real.all;

entity ConfigurableProcessor is
    generic (
        -- Core configuration
        DATA_WIDTH : integer := 32;
        ENABLE_MULTIPLY : boolean := true;
        ENABLE_DIVIDE : boolean := false;
        ENABLE_FLOAT : boolean := false;
        -- Memory configuration
        CACHE_SIZE : integer := 1024;
        CACHE_ASSOCIATIVITY : integer := 4;
        ENABLE_CACHE : boolean := true;
        -- Pipeline configuration
        PIPELINE_STAGES : integer := 5;
        ENABLE_FORWARDING : boolean := true;
        ENABLE_BRANCH_PREDICTION : boolean := true;
        PREDICTOR_TYPE : string := "2bit";
        -- Debug configuration
        ENABLE_DEBUG : boolean := false;
        DEBUG_TRACE_DEPTH : integer := 256
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Instruction interface
        inst_addr : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        inst_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        inst_valid : in std_logic;
        -- Data interface
        data_addr : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        data_out : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        data_in : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        data_we : out std_logic;
        data_re : out std_logic;
        -- Interrupt interface
        irq : in std_logic_vector(7 downto 0);
        irq_ack : out std_logic_vector(7 downto 0);
        -- Debug interface (conditional)
        debug_pc : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        debug_inst : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        debug_valid : out std_logic
    );
end ConfigurableProcessor;

architecture Configurable of ConfigurableProcessor is

    -- Helper function for log2
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

    -- Constants derived from generics
    constant CACHE_INDEX_BITS : integer := clog2(CACHE_SIZE / CACHE_ASSOCIATIVITY);
    constant CACHE_TAG_BITS : integer := DATA_WIDTH - CACHE_INDEX_BITS - 2;

    -- Register file signals
    signal reg_file : std_logic_vector((32 * DATA_WIDTH) - 1 downto 0) := (others => '0');

    -- Pipeline registers
    signal pc : std_logic_vector(DATA_WIDTH - 1 downto 0) := (others => '0');
    signal pc_next : std_logic_vector(DATA_WIDTH - 1 downto 0);

    -- ALU signals
    signal alu_a, alu_b, alu_result : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal alu_op : std_logic_vector(3 downto 0);

    -- Multiply signals (conditional)
    signal mult_a, mult_b : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal mult_result : std_logic_vector((2 * DATA_WIDTH) - 1 downto 0);
    signal mult_valid : std_logic;

    -- Divide signals (conditional)
    signal div_a, div_b : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal div_quotient, div_remainder : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal div_valid : std_logic;

    -- Debug signals (conditional)
    signal debug_trace : std_logic_vector((DEBUG_TRACE_DEPTH * DATA_WIDTH) - 1 downto 0);
    signal debug_ptr : unsigned(clog2(DEBUG_TRACE_DEPTH) - 1 downto 0) := (others => '0');

begin

    -- ==========================================================================
    -- Multiplier Generation (conditional on ENABLE_MULTIPLY)
    -- ==========================================================================
    gen_multiplier : if ENABLE_MULTIPLY generate
        signal mult_stage1 : std_logic_vector((2 * DATA_WIDTH) - 1 downto 0);
        signal mult_stage2 : std_logic_vector((2 * DATA_WIDTH) - 1 downto 0);
    begin
        -- Generate pipelined or combinational multiplier based on width
        gen_small_mult : if DATA_WIDTH <= 16 generate
            -- Combinational multiplier for small widths
            mult_result <= std_logic_vector(unsigned(mult_a) * unsigned(mult_b));
            mult_valid <= '1';
        end generate gen_small_mult;

        gen_large_mult : if DATA_WIDTH > 16 generate
            -- Pipelined multiplier for large widths
            mult_pipeline : process(clk, rst)
            begin
                if rst = '1' then
                    mult_stage1 <= (others => '0');
                    mult_stage2 <= (others => '0');
                    mult_valid <= '0';
                elsif rising_edge(clk) then
                    mult_stage1 <= std_logic_vector(unsigned(mult_a) * unsigned(mult_b));
                    mult_stage2 <= mult_stage1;
                    mult_valid <= '1';
                end if;
            end process mult_pipeline;
            mult_result <= mult_stage2;
        end generate gen_large_mult;
    end generate gen_multiplier;

    gen_no_multiplier : if not ENABLE_MULTIPLY generate
        mult_result <= (others => '0');
        mult_valid <= '0';
    end generate gen_no_multiplier;

    -- ==========================================================================
    -- Divider Generation (conditional on ENABLE_DIVIDE)
    -- ==========================================================================
    gen_divider : if ENABLE_DIVIDE generate
        signal div_counter : unsigned(clog2(DATA_WIDTH) downto 0);
        signal div_busy : std_logic := '0';
        signal div_accumulator : unsigned(DATA_WIDTH - 1 downto 0);
        signal div_remainder_reg : unsigned(DATA_WIDTH - 1 downto 0);
    begin
        div_process : process(clk, rst)
        begin
            if rst = '1' then
                div_counter <= (others => '0');
                div_busy <= '0';
                div_valid <= '0';
                div_quotient <= (others => '0');
                div_remainder <= (others => '0');
            elsif rising_edge(clk) then
                if div_busy = '0' then
                    if unsigned(div_b) /= 0 then
                        div_accumulator <= unsigned(div_a);
                        div_remainder_reg <= (others => '0');
                        div_counter <= to_unsigned(DATA_WIDTH, clog2(DATA_WIDTH) + 1);
                        div_busy <= '1';
                        div_valid <= '0';
                    end if;
                else
                    if div_counter > 0 then
                        div_counter <= div_counter - 1;
                    else
                        div_quotient <= std_logic_vector(div_accumulator);
                        div_remainder <= std_logic_vector(div_remainder_reg);
                        div_busy <= '0';
                        div_valid <= '1';
                    end if;
                end if;
            end if;
        end process div_process;
    end generate gen_divider;

    gen_no_divider : if not ENABLE_DIVIDE generate
        div_quotient <= (others => '0');
        div_remainder <= (others => '0');
        div_valid <= '0';
    end generate gen_no_divider;

    -- ==========================================================================
    -- Cache Generation (conditional on ENABLE_CACHE)
    -- ==========================================================================
    gen_cache : if ENABLE_CACHE generate
        constant CACHE_LINES : integer := CACHE_SIZE / (DATA_WIDTH / 8);
        constant SETS : integer := CACHE_LINES / CACHE_ASSOCIATIVITY;

        signal cache_data : std_logic_vector((CACHE_SIZE * 8) - 1 downto 0) := (others => '0');
        signal cache_tags : std_logic_vector((SETS * CACHE_ASSOCIATIVITY * CACHE_TAG_BITS) - 1 downto 0) := (others => '0');
        signal cache_valid : std_logic_vector((SETS * CACHE_ASSOCIATIVITY) - 1 downto 0) := (others => '0');
        signal cache_lru : std_logic_vector((SETS * clog2(CACHE_ASSOCIATIVITY)) - 1 downto 0) := (others => '0');
    begin
        -- Generate cache ways
        gen_cache_ways : for way in 0 to CACHE_ASSOCIATIVITY - 1 generate
            signal way_hit : std_logic;
            signal way_data : std_logic_vector(DATA_WIDTH - 1 downto 0);
        begin
            -- Generate cache sets within each way
            gen_cache_sets : for set_idx in 0 to SETS - 1 generate
                signal set_tag : std_logic_vector(CACHE_TAG_BITS - 1 downto 0);
                signal set_valid : std_logic;
                signal set_data : std_logic_vector(DATA_WIDTH - 1 downto 0);
            begin
                -- Tag comparison logic
                set_tag <= cache_tags(((set_idx * CACHE_ASSOCIATIVITY + way + 1) * CACHE_TAG_BITS) - 1 downto
                                      (set_idx * CACHE_ASSOCIATIVITY + way) * CACHE_TAG_BITS);
                set_valid <= cache_valid(set_idx * CACHE_ASSOCIATIVITY + way);
            end generate gen_cache_sets;
        end generate gen_cache_ways;
    end generate gen_cache;

    gen_no_cache : if not ENABLE_CACHE generate
        -- Direct memory access without cache
    begin
        data_addr <= alu_result;
    end generate gen_no_cache;

    -- ==========================================================================
    -- Pipeline Generation (variable stages)
    -- ==========================================================================
    gen_pipeline : for stage in 0 to PIPELINE_STAGES - 1 generate
        signal stage_valid : std_logic := '0';
        signal stage_pc : std_logic_vector(DATA_WIDTH - 1 downto 0) := (others => '0');
        signal stage_inst : std_logic_vector(DATA_WIDTH - 1 downto 0) := (others => '0');
    begin
        -- First stage (fetch)
        gen_fetch_stage : if stage = 0 generate
            fetch_proc : process(clk, rst)
            begin
                if rst = '1' then
                    stage_valid <= '0';
                    stage_pc <= (others => '0');
                elsif rising_edge(clk) then
                    stage_valid <= inst_valid;
                    stage_pc <= pc;
                    stage_inst <= inst_data;
                end if;
            end process fetch_proc;
        end generate gen_fetch_stage;

        -- Middle stages (decode, execute, memory)
        gen_middle_stages : if stage > 0 and stage < PIPELINE_STAGES - 1 generate
            middle_proc : process(clk, rst)
            begin
                if rst = '1' then
                    stage_valid <= '0';
                elsif rising_edge(clk) then
                    stage_valid <= stage_valid;
                end if;
            end process middle_proc;
        end generate gen_middle_stages;

        -- Last stage (writeback)
        gen_writeback_stage : if stage = PIPELINE_STAGES - 1 generate
            writeback_proc : process(clk, rst)
            begin
                if rst = '1' then
                    stage_valid <= '0';
                elsif rising_edge(clk) then
                    stage_valid <= stage_valid;
                end if;
            end process writeback_proc;
        end generate gen_writeback_stage;
    end generate gen_pipeline;

    -- ==========================================================================
    -- Branch Prediction (conditional)
    -- ==========================================================================
    gen_branch_prediction : if ENABLE_BRANCH_PREDICTION generate
        constant BHT_SIZE : integer := 256;
        signal bht : std_logic_vector((BHT_SIZE * 2) - 1 downto 0) := (others => '0');
        signal btb_pc : std_logic_vector((BHT_SIZE * DATA_WIDTH) - 1 downto 0) := (others => '0');
        signal btb_target : std_logic_vector((BHT_SIZE * DATA_WIDTH) - 1 downto 0) := (others => '0');
    begin
        -- Generate predictor type
        gen_1bit_predictor : if PREDICTOR_TYPE = "1bit" generate
            predict_1bit : process(clk, rst)
                variable idx : integer;
            begin
                if rst = '1' then
                    bht <= (others => '0');
                elsif rising_edge(clk) then
                    idx := to_integer(unsigned(pc(clog2(BHT_SIZE) + 1 downto 2)));
                end if;
            end process predict_1bit;
        end generate gen_1bit_predictor;

        gen_2bit_predictor : if PREDICTOR_TYPE = "2bit" generate
            predict_2bit : process(clk, rst)
                variable idx : integer;
                variable counter : unsigned(1 downto 0);
            begin
                if rst = '1' then
                    bht <= (others => '0');
                elsif rising_edge(clk) then
                    idx := to_integer(unsigned(pc(clog2(BHT_SIZE) + 1 downto 2)));
                    counter := unsigned(bht((idx + 1) * 2 - 1 downto idx * 2));
                end if;
            end process predict_2bit;
        end generate gen_2bit_predictor;

        gen_gshare_predictor : if PREDICTOR_TYPE = "gshare" generate
            signal global_history : std_logic_vector(7 downto 0) := (others => '0');
        begin
            predict_gshare : process(clk, rst)
                variable idx : integer;
            begin
                if rst = '1' then
                    bht <= (others => '0');
                    global_history <= (others => '0');
                elsif rising_edge(clk) then
                    idx := to_integer(unsigned(pc(9 downto 2) xor global_history));
                end if;
            end process predict_gshare;
        end generate gen_gshare_predictor;
    end generate gen_branch_prediction;

    gen_no_branch_prediction : if not ENABLE_BRANCH_PREDICTION generate
        pc_next <= std_logic_vector(unsigned(pc) + 4);
    end generate gen_no_branch_prediction;

    -- ==========================================================================
    -- Forwarding Logic (conditional)
    -- ==========================================================================
    gen_forwarding : if ENABLE_FORWARDING generate
        signal forward_a, forward_b : std_logic_vector(1 downto 0);
        signal forward_data_ex, forward_data_mem : std_logic_vector(DATA_WIDTH - 1 downto 0);
    begin
        -- Generate forwarding paths for each pipeline stage
        gen_forward_paths : for i in 1 to PIPELINE_STAGES - 2 generate
        begin
        end generate gen_forward_paths;
    end generate gen_forwarding;

    -- ==========================================================================
    -- Debug Logic (conditional)
    -- ==========================================================================
    gen_debug : if ENABLE_DEBUG generate
        signal trace_buffer : std_logic_vector((DEBUG_TRACE_DEPTH * 2 * DATA_WIDTH) - 1 downto 0) := (others => '0');
        signal trace_write_ptr : unsigned(clog2(DEBUG_TRACE_DEPTH) - 1 downto 0) := (others => '0');
        signal trace_count : unsigned(clog2(DEBUG_TRACE_DEPTH) downto 0) := (others => '0');
    begin
        debug_trace_proc : process(clk, rst)
        begin
            if rst = '1' then
                trace_write_ptr <= (others => '0');
                trace_count <= (others => '0');
                debug_valid <= '0';
            elsif rising_edge(clk) then
                debug_pc <= pc;
                debug_inst <= inst_data;
                debug_valid <= inst_valid;

                if inst_valid = '1' then
                    trace_buffer(to_integer(trace_write_ptr) * 2 * DATA_WIDTH + DATA_WIDTH - 1 downto
                                 to_integer(trace_write_ptr) * 2 * DATA_WIDTH) <= pc;
                    trace_buffer(to_integer(trace_write_ptr) * 2 * DATA_WIDTH + 2 * DATA_WIDTH - 1 downto
                                 to_integer(trace_write_ptr) * 2 * DATA_WIDTH + DATA_WIDTH) <= inst_data;

                    if trace_write_ptr = DEBUG_TRACE_DEPTH - 1 then
                        trace_write_ptr <= (others => '0');
                    else
                        trace_write_ptr <= trace_write_ptr + 1;
                    end if;

                    if trace_count < DEBUG_TRACE_DEPTH then
                        trace_count <= trace_count + 1;
                    end if;
                end if;
            end if;
        end process debug_trace_proc;
    end generate gen_debug;

    gen_no_debug : if not ENABLE_DEBUG generate
        debug_pc <= (others => '0');
        debug_inst <= (others => '0');
        debug_valid <= '0';
    end generate gen_no_debug;

    -- ==========================================================================
    -- Floating Point Unit (conditional, complex)
    -- ==========================================================================
    gen_fpu : if ENABLE_FLOAT generate
        constant FP_EXP_BITS : integer := 8;
        constant FP_MANT_BITS : integer := 23;

        signal fp_a, fp_b, fp_result : std_logic_vector(31 downto 0);
        signal fp_op : std_logic_vector(2 downto 0);
        signal fp_valid : std_logic;

        -- Generate FPU subunits
    begin
        -- FP Adder
        gen_fp_adder : if DATA_WIDTH >= 32 generate
        begin
        end generate gen_fp_adder;

        -- FP Multiplier
        gen_fp_mult : if DATA_WIDTH >= 32 and ENABLE_MULTIPLY generate
        begin
        end generate gen_fp_mult;

        -- FP Divider
        gen_fp_div : if DATA_WIDTH >= 32 and ENABLE_DIVIDE generate
        begin
        end generate gen_fp_div;
    end generate gen_fpu;

    -- Basic outputs
    inst_addr <= pc;
    data_out <= alu_result;
    data_we <= '0';
    data_re <= '0';
    irq_ack <= (others => '0');

end Configurable;
