-- Test 11: Generate Statements (for-generate and if-generate)
-- Tests: For-generate, if-generate, nested generates, labels, generate with components

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity ParallelAdderTree is
    generic (
        NUM_INPUTS : integer := 8;
        DATA_WIDTH : integer := 16
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        inputs : in std_logic_vector((NUM_INPUTS * DATA_WIDTH) - 1 downto 0);
        valid_in : in std_logic;
        sum_out : out std_logic_vector(DATA_WIDTH + clog2(NUM_INPUTS) - 1 downto 0);
        valid_out : out std_logic
    );
end ParallelAdderTree;

architecture Structural of ParallelAdderTree is

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

    constant TREE_DEPTH : integer := clog2(NUM_INPUTS);

    component Adder is
        generic (
            WIDTH : integer := 16
        );
        port (
            a : in std_logic_vector(WIDTH - 1 downto 0);
            b : in std_logic_vector(WIDTH - 1 downto 0);
            sum : out std_logic_vector(WIDTH downto 0)
        );
    end component;

    component PipelineReg is
        generic (
            WIDTH : integer := 16
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            d : in std_logic_vector(WIDTH - 1 downto 0);
            q : out std_logic_vector(WIDTH - 1 downto 0)
        );
    end component;

    signal stage0 : std_logic_vector((NUM_INPUTS * DATA_WIDTH) - 1 downto 0);
    signal stage1 : std_logic_vector(((NUM_INPUTS / 2) * (DATA_WIDTH + 1)) - 1 downto 0);
    signal stage2 : std_logic_vector(((NUM_INPUTS / 4) * (DATA_WIDTH + 2)) - 1 downto 0);
    signal stage3 : std_logic_vector(((NUM_INPUTS / 8) * (DATA_WIDTH + 3)) - 1 downto 0);
    signal valid_pipe : std_logic_vector(TREE_DEPTH - 1 downto 0) := (others => '0');

begin

    -- Input registration
    stage0 <= inputs;

    -- Generate first level of adders (NUM_INPUTS/2 adders)
    gen_level1 : for i in 0 to (NUM_INPUTS / 2) - 1 generate
        signal a_in, b_in : std_logic_vector(DATA_WIDTH - 1 downto 0);
        signal sum_temp : std_logic_vector(DATA_WIDTH downto 0);
    begin
        a_in <= stage0(((i * 2) + 1) * DATA_WIDTH - 1 downto (i * 2) * DATA_WIDTH);
        b_in <= stage0(((i * 2) + 2) * DATA_WIDTH - 1 downto ((i * 2) + 1) * DATA_WIDTH);

        adder_inst : Adder
            generic map (
                WIDTH => DATA_WIDTH
            )
            port map (
                a => a_in,
                b => b_in,
                sum => sum_temp
            );

        stage1((i + 1) * (DATA_WIDTH + 1) - 1 downto i * (DATA_WIDTH + 1)) <= sum_temp;
    end generate gen_level1;

    -- Generate second level if needed
    gen_level2 : if NUM_INPUTS >= 4 generate
        gen_level2_adders : for i in 0 to (NUM_INPUTS / 4) - 1 generate
            signal a_in2, b_in2 : std_logic_vector(DATA_WIDTH downto 0);
            signal sum_temp2 : std_logic_vector(DATA_WIDTH + 1 downto 0);
        begin
            a_in2 <= stage1(((i * 2) + 1) * (DATA_WIDTH + 1) - 1 downto (i * 2) * (DATA_WIDTH + 1));
            b_in2 <= stage1(((i * 2) + 2) * (DATA_WIDTH + 1) - 1 downto ((i * 2) + 1) * (DATA_WIDTH + 1));

            adder_inst2 : Adder
                generic map (
                    WIDTH => DATA_WIDTH + 1
                )
                port map (
                    a => a_in2,
                    b => b_in2,
                    sum => sum_temp2
                );

            stage2((i + 1) * (DATA_WIDTH + 2) - 1 downto i * (DATA_WIDTH + 2)) <= sum_temp2;
        end generate gen_level2_adders;
    end generate gen_level2;

    -- Generate third level if needed
    gen_level3 : if NUM_INPUTS >= 8 generate
        gen_level3_adders : for i in 0 to (NUM_INPUTS / 8) - 1 generate
            signal a_in3, b_in3 : std_logic_vector(DATA_WIDTH + 1 downto 0);
            signal sum_temp3 : std_logic_vector(DATA_WIDTH + 2 downto 0);
        begin
            a_in3 <= stage2(((i * 2) + 1) * (DATA_WIDTH + 2) - 1 downto (i * 2) * (DATA_WIDTH + 2));
            b_in3 <= stage2(((i * 2) + 2) * (DATA_WIDTH + 2) - 1 downto ((i * 2) + 1) * (DATA_WIDTH + 2));

            adder_inst3 : Adder
                generic map (
                    WIDTH => DATA_WIDTH + 2
                )
                port map (
                    a => a_in3,
                    b => b_in3,
                    sum => sum_temp3
                );

            stage3((i + 1) * (DATA_WIDTH + 3) - 1 downto i * (DATA_WIDTH + 3)) <= sum_temp3;
        end generate gen_level3_adders;
    end generate gen_level3;

    -- Valid pipeline
    valid_pipeline : process(clk, rst)
    begin
        if rst = '1' then
            valid_pipe <= (others => '0');
        elsif rising_edge(clk) then
            valid_pipe(0) <= valid_in;
            for i in 1 to TREE_DEPTH - 1 loop
                valid_pipe(i) <= valid_pipe(i - 1);
            end loop;
        end if;
    end process valid_pipeline;

    -- Output assignment based on tree depth
    gen_output_8 : if NUM_INPUTS = 8 generate
        sum_out <= stage3(DATA_WIDTH + 2 downto 0);
    end generate gen_output_8;

    gen_output_4 : if NUM_INPUTS = 4 generate
        sum_out <= stage2(DATA_WIDTH + 1 downto 0);
    end generate gen_output_4;

    gen_output_2 : if NUM_INPUTS = 2 generate
        sum_out <= stage1(DATA_WIDTH downto 0);
    end generate gen_output_2;

    valid_out <= valid_pipe(TREE_DEPTH - 1);

end Structural;

-- Simple adder entity for the tree
entity Adder is
    generic (
        WIDTH : integer := 16
    );
    port (
        a : in std_logic_vector(WIDTH - 1 downto 0);
        b : in std_logic_vector(WIDTH - 1 downto 0);
        sum : out std_logic_vector(WIDTH downto 0)
    );
end Adder;

architecture Behavioral of Adder is
begin
    sum <= std_logic_vector(unsigned('0' & a) + unsigned('0' & b));
end Behavioral;

-- Pipeline register entity
entity PipelineReg is
    generic (
        WIDTH : integer := 16
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        d : in std_logic_vector(WIDTH - 1 downto 0);
        q : out std_logic_vector(WIDTH - 1 downto 0)
    );
end PipelineReg;

architecture Behavioral of PipelineReg is
begin
    process(clk, rst)
    begin
        if rst = '1' then
            q <= (others => '0');
        elsif rising_edge(clk) then
            q <= d;
        end if;
    end process;
end Behavioral;
