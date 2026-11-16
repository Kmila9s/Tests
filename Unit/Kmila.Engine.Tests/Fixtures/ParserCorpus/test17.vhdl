-- Test 17: Complex Component Hierarchy with Deep Nesting
-- Tests: Multiple levels of component instantiation, generic propagation, port maps, signal routing

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

-- Level 4: Basic building block - D Flip-Flop
entity DFF is
    port (
        clk : in std_logic;
        rst : in std_logic;
        d : in std_logic;
        q : out std_logic;
        q_n : out std_logic
    );
end DFF;

architecture RTL of DFF is
    signal q_int : std_logic := '0';
begin
    process(clk, rst)
    begin
        if rst = '1' then
            q_int <= '0';
        elsif rising_edge(clk) then
            q_int <= d;
        end if;
    end process;
    q <= q_int;
    q_n <= not q_int;
end RTL;

-- Level 3: Register built from DFFs
entity Register is
    generic (
        WIDTH : integer := 8
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        load : in std_logic;
        d : in std_logic_vector(WIDTH - 1 downto 0);
        q : out std_logic_vector(WIDTH - 1 downto 0)
    );
end Register;

architecture Structural of Register is
    component DFF is
        port (
            clk : in std_logic;
            rst : in std_logic;
            d : in std_logic;
            q : out std_logic;
            q_n : out std_logic
        );
    end component;

    signal d_mux : std_logic_vector(WIDTH - 1 downto 0);
    signal q_int : std_logic_vector(WIDTH - 1 downto 0);
    signal q_n_unused : std_logic_vector(WIDTH - 1 downto 0);
begin
    -- Generate DFFs for each bit
    gen_dffs : for i in 0 to WIDTH - 1 generate
        d_mux(i) <= d(i) when load = '1' else q_int(i);

        dff_inst : DFF
            port map (
                clk => clk,
                rst => rst,
                d => d_mux(i),
                q => q_int(i),
                q_n => q_n_unused(i)
            );
    end generate gen_dffs;

    q <= q_int;
end Structural;

-- Level 3: ALU built from basic gates
entity ALU is
    generic (
        WIDTH : integer := 8
    );
    port (
        a : in std_logic_vector(WIDTH - 1 downto 0);
        b : in std_logic_vector(WIDTH - 1 downto 0);
        op : in std_logic_vector(2 downto 0);
        result : out std_logic_vector(WIDTH - 1 downto 0);
        zero : out std_logic;
        carry : out std_logic;
        overflow : out std_logic
    );
end ALU;

architecture RTL of ALU is
    signal result_int : std_logic_vector(WIDTH downto 0);
    signal result_out : std_logic_vector(WIDTH - 1 downto 0);
begin
    process(a, b, op)
        variable temp : unsigned(WIDTH downto 0);
    begin
        carry <= '0';
        overflow <= '0';

        case op is
            when "000" =>
                temp := ('0' & unsigned(a)) + ('0' & unsigned(b));
                result_int <= std_logic_vector(temp);
                carry <= temp(WIDTH);
            when "001" =>
                temp := ('0' & unsigned(a)) - ('0' & unsigned(b));
                result_int <= std_logic_vector(temp);
                carry <= temp(WIDTH);
            when "010" =>
                result_int <= '0' & (a and b);
            when "011" =>
                result_int <= '0' & (a or b);
            when "100" =>
                result_int <= '0' & (a xor b);
            when "101" =>
                result_int <= '0' & (not a);
            when "110" =>
                result_int <= a & '0';
            when others =>
                result_int <= '0' & a(WIDTH - 1 downto 1);
        end case;
    end process;

    result_out <= result_int(WIDTH - 1 downto 0);
    result <= result_out;
    zero <= '1' when result_out = (result_out'range => '0') else '0';
end RTL;

-- Level 2: Register File
entity RegisterFile is
    generic (
        DATA_WIDTH : integer := 8;
        ADDR_WIDTH : integer := 3;
        NUM_REGS : integer := 8
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Write port
        wr_en : in std_logic;
        wr_addr : in std_logic_vector(ADDR_WIDTH - 1 downto 0);
        wr_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        -- Read ports
        rd_addr_a : in std_logic_vector(ADDR_WIDTH - 1 downto 0);
        rd_addr_b : in std_logic_vector(ADDR_WIDTH - 1 downto 0);
        rd_data_a : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        rd_data_b : out std_logic_vector(DATA_WIDTH - 1 downto 0)
    );
end RegisterFile;

architecture Structural of RegisterFile is
    component Register is
        generic (
            WIDTH : integer := 8
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            load : in std_logic;
            d : in std_logic_vector(WIDTH - 1 downto 0);
            q : out std_logic_vector(WIDTH - 1 downto 0)
        );
    end component;

    type reg_array_t is array (0 to NUM_REGS - 1) of std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal reg_outputs : reg_array_t;
    signal reg_loads : std_logic_vector(NUM_REGS - 1 downto 0);
begin
    -- Generate registers
    gen_regs : for i in 0 to NUM_REGS - 1 generate
        reg_loads(i) <= '1' when (wr_en = '1' and unsigned(wr_addr) = i) else '0';

        reg_inst : Register
            generic map (
                WIDTH => DATA_WIDTH
            )
            port map (
                clk => clk,
                rst => rst,
                load => reg_loads(i),
                d => wr_data,
                q => reg_outputs(i)
            );
    end generate gen_regs;

    -- Read multiplexers
    rd_data_a <= reg_outputs(to_integer(unsigned(rd_addr_a)));
    rd_data_b <= reg_outputs(to_integer(unsigned(rd_addr_b)));
end Structural;

-- Level 2: Execution Unit
entity ExecutionUnit is
    generic (
        DATA_WIDTH : integer := 8
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        op_a : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        op_b : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        alu_op : in std_logic_vector(2 downto 0);
        bypass_a : in std_logic;
        bypass_b : in std_logic;
        bypass_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        result : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        flags : out std_logic_vector(2 downto 0)
    );
end ExecutionUnit;

architecture Structural of ExecutionUnit is
    component ALU is
        generic (
            WIDTH : integer := 8
        );
        port (
            a : in std_logic_vector(WIDTH - 1 downto 0);
            b : in std_logic_vector(WIDTH - 1 downto 0);
            op : in std_logic_vector(2 downto 0);
            result : out std_logic_vector(WIDTH - 1 downto 0);
            zero : out std_logic;
            carry : out std_logic;
            overflow : out std_logic
        );
    end component;

    component Register is
        generic (
            WIDTH : integer := 8
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            load : in std_logic;
            d : in std_logic_vector(WIDTH - 1 downto 0);
            q : out std_logic_vector(WIDTH - 1 downto 0)
        );
    end component;

    signal mux_a, mux_b : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal alu_result : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal zero, carry, overflow : std_logic;
begin
    -- Bypass multiplexers
    mux_a <= bypass_data when bypass_a = '1' else op_a;
    mux_b <= bypass_data when bypass_b = '1' else op_b;

    -- ALU instance
    alu_inst : ALU
        generic map (
            WIDTH => DATA_WIDTH
        )
        port map (
            a => mux_a,
            b => mux_b,
            op => alu_op,
            result => alu_result,
            zero => zero,
            carry => carry,
            overflow => overflow
        );

    -- Output register
    result_reg : Register
        generic map (
            WIDTH => DATA_WIDTH
        )
        port map (
            clk => clk,
            rst => rst,
            load => '1',
            d => alu_result,
            q => result
        );

    flags <= overflow & carry & zero;
end Structural;

-- Level 1: Datapath
entity Datapath is
    generic (
        DATA_WIDTH : integer := 8;
        REG_ADDR_WIDTH : integer := 3
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Control signals
        reg_wr_en : in std_logic;
        alu_op : in std_logic_vector(2 downto 0);
        -- Addresses
        rs_addr : in std_logic_vector(REG_ADDR_WIDTH - 1 downto 0);
        rt_addr : in std_logic_vector(REG_ADDR_WIDTH - 1 downto 0);
        rd_addr : in std_logic_vector(REG_ADDR_WIDTH - 1 downto 0);
        -- Data paths
        imm_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        use_imm : in std_logic;
        -- Outputs
        alu_flags : out std_logic_vector(2 downto 0);
        result_out : out std_logic_vector(DATA_WIDTH - 1 downto 0)
    );
end Datapath;

architecture Structural of Datapath is
    component RegisterFile is
        generic (
            DATA_WIDTH : integer := 8;
            ADDR_WIDTH : integer := 3;
            NUM_REGS : integer := 8
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            wr_en : in std_logic;
            wr_addr : in std_logic_vector(ADDR_WIDTH - 1 downto 0);
            wr_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
            rd_addr_a : in std_logic_vector(ADDR_WIDTH - 1 downto 0);
            rd_addr_b : in std_logic_vector(ADDR_WIDTH - 1 downto 0);
            rd_data_a : out std_logic_vector(DATA_WIDTH - 1 downto 0);
            rd_data_b : out std_logic_vector(DATA_WIDTH - 1 downto 0)
        );
    end component;

    component ExecutionUnit is
        generic (
            DATA_WIDTH : integer := 8
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            op_a : in std_logic_vector(DATA_WIDTH - 1 downto 0);
            op_b : in std_logic_vector(DATA_WIDTH - 1 downto 0);
            alu_op : in std_logic_vector(2 downto 0);
            bypass_a : in std_logic;
            bypass_b : in std_logic;
            bypass_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
            result : out std_logic_vector(DATA_WIDTH - 1 downto 0);
            flags : out std_logic_vector(2 downto 0)
        );
    end component;

    signal rs_data, rt_data : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal op_b_mux : std_logic_vector(DATA_WIDTH - 1 downto 0);
    signal exec_result : std_logic_vector(DATA_WIDTH - 1 downto 0);
begin
    -- Register file
    regfile : RegisterFile
        generic map (
            DATA_WIDTH => DATA_WIDTH,
            ADDR_WIDTH => REG_ADDR_WIDTH,
            NUM_REGS => 2 ** REG_ADDR_WIDTH
        )
        port map (
            clk => clk,
            rst => rst,
            wr_en => reg_wr_en,
            wr_addr => rd_addr,
            wr_data => exec_result,
            rd_addr_a => rs_addr,
            rd_addr_b => rt_addr,
            rd_data_a => rs_data,
            rd_data_b => rt_data
        );

    -- Immediate multiplexer
    op_b_mux <= imm_data when use_imm = '1' else rt_data;

    -- Execution unit
    exec_unit : ExecutionUnit
        generic map (
            DATA_WIDTH => DATA_WIDTH
        )
        port map (
            clk => clk,
            rst => rst,
            op_a => rs_data,
            op_b => op_b_mux,
            alu_op => alu_op,
            bypass_a => '0',
            bypass_b => '0',
            bypass_data => (others => '0'),
            result => exec_result,
            flags => alu_flags
        );

    result_out <= exec_result;
end Structural;

-- Level 0: Top-level CPU
entity SimpleCPU is
    generic (
        DATA_WIDTH : integer := 8;
        INST_WIDTH : integer := 16;
        ADDR_WIDTH : integer := 10
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        -- Instruction memory interface
        inst_addr : out std_logic_vector(ADDR_WIDTH - 1 downto 0);
        inst_data : in std_logic_vector(INST_WIDTH - 1 downto 0);
        -- Data memory interface
        data_addr : out std_logic_vector(ADDR_WIDTH - 1 downto 0);
        data_out : out std_logic_vector(DATA_WIDTH - 1 downto 0);
        data_in : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        data_wr : out std_logic;
        -- Status
        halted : out std_logic
    );
end SimpleCPU;

architecture Structural of SimpleCPU is
    component Datapath is
        generic (
            DATA_WIDTH : integer := 8;
            REG_ADDR_WIDTH : integer := 3
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            reg_wr_en : in std_logic;
            alu_op : in std_logic_vector(2 downto 0);
            rs_addr : in std_logic_vector(REG_ADDR_WIDTH - 1 downto 0);
            rt_addr : in std_logic_vector(REG_ADDR_WIDTH - 1 downto 0);
            rd_addr : in std_logic_vector(REG_ADDR_WIDTH - 1 downto 0);
            imm_data : in std_logic_vector(DATA_WIDTH - 1 downto 0);
            use_imm : in std_logic;
            alu_flags : out std_logic_vector(2 downto 0);
            result_out : out std_logic_vector(DATA_WIDTH - 1 downto 0)
        );
    end component;

    component Register is
        generic (
            WIDTH : integer := 8
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            load : in std_logic;
            d : in std_logic_vector(WIDTH - 1 downto 0);
            q : out std_logic_vector(WIDTH - 1 downto 0)
        );
    end component;

    -- Program counter
    signal pc : std_logic_vector(ADDR_WIDTH - 1 downto 0);
    signal pc_next : std_logic_vector(ADDR_WIDTH - 1 downto 0);
    signal pc_load : std_logic;

    -- Instruction decode
    signal opcode : std_logic_vector(3 downto 0);
    signal rs_addr, rt_addr, rd_addr : std_logic_vector(2 downto 0);
    signal immediate : std_logic_vector(DATA_WIDTH - 1 downto 0);

    -- Control signals
    signal reg_wr_en : std_logic;
    signal alu_op : std_logic_vector(2 downto 0);
    signal use_imm : std_logic;
    signal halt_sig : std_logic := '0';

    -- Datapath signals
    signal alu_flags : std_logic_vector(2 downto 0);
    signal alu_result : std_logic_vector(DATA_WIDTH - 1 downto 0);
begin
    -- Program counter register
    pc_reg : Register
        generic map (
            WIDTH => ADDR_WIDTH
        )
        port map (
            clk => clk,
            rst => rst,
            load => pc_load,
            d => pc_next,
            q => pc
        );

    -- PC increment
    pc_next <= std_logic_vector(unsigned(pc) + 1);
    pc_load <= not halt_sig;
    inst_addr <= pc;

    -- Instruction decode
    opcode <= inst_data(INST_WIDTH - 1 downto INST_WIDTH - 4);
    rd_addr <= inst_data(INST_WIDTH - 5 downto INST_WIDTH - 7);
    rs_addr <= inst_data(INST_WIDTH - 8 downto INST_WIDTH - 10);
    rt_addr <= inst_data(INST_WIDTH - 11 downto INST_WIDTH - 13);
    immediate <= inst_data(DATA_WIDTH - 1 downto 0);

    -- Control logic
    process(opcode)
    begin
        reg_wr_en <= '0';
        alu_op <= "000";
        use_imm <= '0';
        halt_sig <= '0';
        data_wr <= '0';

        case opcode is
            when "0000" => reg_wr_en <= '1'; alu_op <= "000";
            when "0001" => reg_wr_en <= '1'; alu_op <= "001";
            when "0010" => reg_wr_en <= '1'; alu_op <= "010";
            when "0011" => reg_wr_en <= '1'; alu_op <= "011";
            when "0100" => reg_wr_en <= '1'; alu_op <= "100";
            when "0101" => reg_wr_en <= '1'; alu_op <= "000"; use_imm <= '1';
            when "1111" => halt_sig <= '1';
            when others => null;
        end case;
    end process;

    -- Datapath instance
    dp : Datapath
        generic map (
            DATA_WIDTH => DATA_WIDTH,
            REG_ADDR_WIDTH => 3
        )
        port map (
            clk => clk,
            rst => rst,
            reg_wr_en => reg_wr_en,
            alu_op => alu_op,
            rs_addr => rs_addr,
            rt_addr => rt_addr,
            rd_addr => rd_addr,
            imm_data => immediate,
            use_imm => use_imm,
            alu_flags => alu_flags,
            result_out => alu_result
        );

    data_addr <= alu_result(ADDR_WIDTH - 1 downto 0);
    data_out <= alu_result;
    halted <= halt_sig;
end Structural;
