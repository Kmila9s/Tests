-- Test 13: Multiple Architectures for Same Entity with Configurations
-- Tests: Multiple architectures, configuration declarations, component binding, architecture selection

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

-- Entity declaration for a multiplier
entity Multiplier is
    generic (
        WIDTH : integer := 8
    );
    port (
        clk : in std_logic;
        rst : in std_logic;
        a : in std_logic_vector(WIDTH - 1 downto 0);
        b : in std_logic_vector(WIDTH - 1 downto 0);
        start : in std_logic;
        done : out std_logic;
        product : out std_logic_vector((2 * WIDTH) - 1 downto 0)
    );
end Multiplier;

-- Architecture 1: Combinational (single cycle, large area)
architecture Combinational of Multiplier is
begin
    process(clk, rst)
    begin
        if rst = '1' then
            product <= (others => '0');
            done <= '0';
        elsif rising_edge(clk) then
            if start = '1' then
                product <= std_logic_vector(unsigned(a) * unsigned(b));
                done <= '1';
            else
                done <= '0';
            end if;
        end if;
    end process;
end Combinational;

-- Architecture 2: Sequential shift-add (multiple cycles, small area)
architecture ShiftAdd of Multiplier is
    signal multiplicand : std_logic_vector((2 * WIDTH) - 1 downto 0) := (others => '0');
    signal multiplier_reg : std_logic_vector(WIDTH - 1 downto 0) := (others => '0');
    signal accumulator : std_logic_vector((2 * WIDTH) - 1 downto 0) := (others => '0');
    signal counter : unsigned(clog2(WIDTH) downto 0) := (others => '0');
    signal busy : std_logic := '0';

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

begin
    process(clk, rst)
    begin
        if rst = '1' then
            multiplicand <= (others => '0');
            multiplier_reg <= (others => '0');
            accumulator <= (others => '0');
            counter <= (others => '0');
            busy <= '0';
            done <= '0';
            product <= (others => '0');
        elsif rising_edge(clk) then
            done <= '0';

            if start = '1' and busy = '0' then
                multiplicand <= (others => '0');
                multiplicand(WIDTH - 1 downto 0) <= a;
                multiplier_reg <= b;
                accumulator <= (others => '0');
                counter <= to_unsigned(WIDTH, clog2(WIDTH) + 1);
                busy <= '1';
            elsif busy = '1' then
                if counter > 0 then
                    if multiplier_reg(0) = '1' then
                        accumulator <= std_logic_vector(unsigned(accumulator) + unsigned(multiplicand));
                    end if;
                    multiplicand <= multiplicand((2 * WIDTH) - 2 downto 0) & '0';
                    multiplier_reg <= '0' & multiplier_reg(WIDTH - 1 downto 1);
                    counter <= counter - 1;
                else
                    product <= accumulator;
                    done <= '1';
                    busy <= '0';
                end if;
            end if;
        end if;
    end process;
end ShiftAdd;

-- Architecture 3: Booth's algorithm (efficient for signed multiplication)
architecture Booth of Multiplier is
    signal m_reg : signed((2 * WIDTH) downto 0) := (others => '0');
    signal q_reg : signed(WIDTH downto 0) := (others => '0');
    signal count : unsigned(clog2(WIDTH) downto 0) := (others => '0');
    signal busy : std_logic := '0';
    signal neg_a : signed(WIDTH downto 0) := (others => '0');
    signal pos_a : signed(WIDTH downto 0) := (others => '0');

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

begin
    pos_a <= resize(signed(a), WIDTH + 1);
    neg_a <= -resize(signed(a), WIDTH + 1);

    process(clk, rst)
        variable q_bits : std_logic_vector(1 downto 0);
    begin
        if rst = '1' then
            m_reg <= (others => '0');
            q_reg <= (others => '0');
            count <= (others => '0');
            busy <= '0';
            done <= '0';
            product <= (others => '0');
        elsif rising_edge(clk) then
            done <= '0';

            if start = '1' and busy = '0' then
                m_reg <= (others => '0');
                q_reg <= signed(b) & '0';
                count <= to_unsigned(WIDTH, clog2(WIDTH) + 1);
                busy <= '1';
            elsif busy = '1' then
                if count > 0 then
                    q_bits := std_logic_vector(q_reg(1 downto 0));

                    case q_bits is
                        when "01" =>
                            m_reg <= m_reg + (resize(pos_a, (2 * WIDTH) + 1) sll to_integer(WIDTH - count));
                        when "10" =>
                            m_reg <= m_reg + (resize(neg_a, (2 * WIDTH) + 1) sll to_integer(WIDTH - count));
                        when others =>
                            null;
                    end case;

                    q_reg <= q_reg srl 1;
                    count <= count - 1;
                else
                    product <= std_logic_vector(m_reg((2 * WIDTH) - 1 downto 0));
                    done <= '1';
                    busy <= '0';
                end if;
            end if;
        end if;
    end process;
end Booth;

-- Architecture 4: Pipelined (high throughput)
architecture Pipelined of Multiplier is
    constant STAGES : integer := 4;

    signal pipe_valid : std_logic_vector(STAGES - 1 downto 0) := (others => '0');
    signal partial0, partial1, partial2, partial3 : std_logic_vector((2 * WIDTH) - 1 downto 0) := (others => '0');
    signal a_pipe : std_logic_vector((STAGES * WIDTH) - 1 downto 0) := (others => '0');
    signal b_pipe : std_logic_vector((STAGES * WIDTH) - 1 downto 0) := (others => '0');
begin
    process(clk, rst)
        variable pp0, pp1, pp2, pp3 : unsigned((2 * WIDTH) - 1 downto 0);
        variable a_quarter : unsigned((WIDTH / 4) - 1 downto 0);
    begin
        if rst = '1' then
            pipe_valid <= (others => '0');
            partial0 <= (others => '0');
            partial1 <= (others => '0');
            partial2 <= (others => '0');
            partial3 <= (others => '0');
            a_pipe <= (others => '0');
            b_pipe <= (others => '0');
            product <= (others => '0');
            done <= '0';
        elsif rising_edge(clk) then
            -- Stage 0: Register inputs and compute first partial product
            pipe_valid(0) <= start;
            a_pipe(WIDTH - 1 downto 0) <= a;
            b_pipe(WIDTH - 1 downto 0) <= b;

            a_quarter := unsigned(a((WIDTH / 4) - 1 downto 0));
            pp0 := resize(a_quarter * unsigned(b), (2 * WIDTH));
            partial0 <= std_logic_vector(pp0);

            -- Stage 1: Second partial product
            pipe_valid(1) <= pipe_valid(0);
            a_pipe((2 * WIDTH) - 1 downto WIDTH) <= a_pipe(WIDTH - 1 downto 0);
            b_pipe((2 * WIDTH) - 1 downto WIDTH) <= b_pipe(WIDTH - 1 downto 0);

            a_quarter := unsigned(a_pipe((WIDTH / 2) - 1 downto (WIDTH / 4)));
            pp1 := resize(a_quarter * unsigned(b_pipe(WIDTH - 1 downto 0)), (2 * WIDTH));
            partial1 <= std_logic_vector(pp1 sll (WIDTH / 4));

            -- Stage 2: Third partial product
            pipe_valid(2) <= pipe_valid(1);
            a_pipe((3 * WIDTH) - 1 downto (2 * WIDTH)) <= a_pipe((2 * WIDTH) - 1 downto WIDTH);
            b_pipe((3 * WIDTH) - 1 downto (2 * WIDTH)) <= b_pipe((2 * WIDTH) - 1 downto WIDTH);

            a_quarter := unsigned(a_pipe(((3 * WIDTH) / 4) - 1 downto (WIDTH / 2)));
            pp2 := resize(a_quarter * unsigned(b_pipe((2 * WIDTH) - 1 downto WIDTH)), (2 * WIDTH));
            partial2 <= std_logic_vector(pp2 sll (WIDTH / 2));

            -- Stage 3: Final partial product and sum
            pipe_valid(3) <= pipe_valid(2);
            a_pipe((4 * WIDTH) - 1 downto (3 * WIDTH)) <= a_pipe((3 * WIDTH) - 1 downto (2 * WIDTH));
            b_pipe((4 * WIDTH) - 1 downto (3 * WIDTH)) <= b_pipe((3 * WIDTH) - 1 downto (2 * WIDTH));

            a_quarter := unsigned(a_pipe(WIDTH - 1 downto ((3 * WIDTH) / 4)));
            pp3 := resize(a_quarter * unsigned(b_pipe((3 * WIDTH) - 1 downto (2 * WIDTH))), (2 * WIDTH));
            partial3 <= std_logic_vector(pp3 sll ((3 * WIDTH) / 4));

            -- Final output
            product <= std_logic_vector(unsigned(partial0) + unsigned(partial1) +
                                        unsigned(partial2) + unsigned(partial3));
            done <= pipe_valid(3);
        end if;
    end process;
end Pipelined;

-- Configuration for selecting Combinational architecture
configuration Multiplier_Fast of Multiplier is
    for Combinational
    end for;
end Multiplier_Fast;

-- Configuration for selecting ShiftAdd architecture
configuration Multiplier_Small of Multiplier is
    for ShiftAdd
    end for;
end Multiplier_Small;

-- Configuration for selecting Booth architecture
configuration Multiplier_Signed of Multiplier is
    for Booth
    end for;
end Multiplier_Signed;

-- Configuration for selecting Pipelined architecture
configuration Multiplier_HighThroughput of Multiplier is
    for Pipelined
    end for;
end Multiplier_HighThroughput;

-- Top-level entity using the multiplier
entity MultiplierSystem is
    generic (
        DATA_WIDTH : integer := 16;
        USE_PIPELINED : boolean := false
    );
    port (
        sys_clk : in std_logic;
        sys_rst : in std_logic;
        operand_a : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        operand_b : in std_logic_vector(DATA_WIDTH - 1 downto 0);
        multiply : in std_logic;
        result : out std_logic_vector((2 * DATA_WIDTH) - 1 downto 0);
        result_valid : out std_logic
    );
end MultiplierSystem;

architecture Structural of MultiplierSystem is
    component Multiplier is
        generic (
            WIDTH : integer := 8
        );
        port (
            clk : in std_logic;
            rst : in std_logic;
            a : in std_logic_vector(WIDTH - 1 downto 0);
            b : in std_logic_vector(WIDTH - 1 downto 0);
            start : in std_logic;
            done : out std_logic;
            product : out std_logic_vector((2 * WIDTH) - 1 downto 0)
        );
    end component;
begin
    mult_inst : Multiplier
        generic map (
            WIDTH => DATA_WIDTH
        )
        port map (
            clk => sys_clk,
            rst => sys_rst,
            a => operand_a,
            b => operand_b,
            start => multiply,
            done => result_valid,
            product => result
        );
end Structural;

-- Configuration binding specific architecture to component
configuration MultiplierSystem_Config of MultiplierSystem is
    for Structural
        for mult_inst : Multiplier
            use configuration work.Multiplier_Small;
        end for;
    end for;
end MultiplierSystem_Config;
