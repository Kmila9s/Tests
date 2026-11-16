-- Test 40: CORDIC Iteration Core (8-stage, sin/cos rotation mode)
-- Curriculum: Sistemas en Chip, Unit 4 + ASM (Unit 5 of Diseño Sistemas Digitales)

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity CORDIC_Core is
    generic (
        WIDTH : integer := 16;
        ITERS : integer := 8
    );
    port (
        clk      : in  std_logic;
        rst      : in  std_logic;
        start    : in  std_logic;
        x_in     : in  std_logic_vector(WIDTH - 1 downto 0);
        y_in     : in  std_logic_vector(WIDTH - 1 downto 0);
        z_in     : in  std_logic_vector(WIDTH - 1 downto 0);
        x_out    : out std_logic_vector(WIDTH - 1 downto 0);
        y_out    : out std_logic_vector(WIDTH - 1 downto 0);
        z_out    : out std_logic_vector(WIDTH - 1 downto 0);
        done     : out std_logic
    );
end CORDIC_Core;

architecture RTL of CORDIC_Core is
    -- Flat ROM of arctan(2^-i) constants for 8 stages.
    constant ATAN_TABLE : std_logic_vector(127 downto 0) :=
        x"3243_1DAC_0FAD_07F5_03FE_01FF_00FF_007F";

    signal x_reg, y_reg, z_reg : signed(WIDTH - 1 downto 0) := (others => '0');
    signal stage   : unsigned(3 downto 0) := (others => '0');
    signal active  : std_logic := '0';
    signal d_sign  : std_logic;
    signal x_shifted, y_shifted : signed(WIDTH - 1 downto 0);
    signal atan_val : signed(WIDTH - 1 downto 0);
begin
    -- Sign of z determines direction of rotation
    d_sign <= z_reg(WIDTH - 1);

    -- Atan lookup (treating ATAN_TABLE as 8 x WIDTH-bit slices)
    atan_val <= signed(ATAN_TABLE((to_integer(stage) + 1) * WIDTH - 1
                                  downto to_integer(stage) * WIDTH));

    -- Shift right by stage count
    shift_proc : process(x_reg, y_reg, stage)
    begin
        x_shifted <= shift_right(x_reg, to_integer(stage));
        y_shifted <= shift_right(y_reg, to_integer(stage));
    end process shift_proc;

    cordic_proc : process(clk, rst)
    begin
        if rst = '1' then
            x_reg  <= (others => '0');
            y_reg  <= (others => '0');
            z_reg  <= (others => '0');
            stage  <= (others => '0');
            active <= '0';
            done   <= '0';
        elsif rising_edge(clk) then
            done <= '0';
            if active = '0' then
                if start = '1' then
                    x_reg  <= signed(x_in);
                    y_reg  <= signed(y_in);
                    z_reg  <= signed(z_in);
                    stage  <= (others => '0');
                    active <= '1';
                end if;
            else
                if d_sign = '0' then
                    x_reg <= x_reg - y_shifted;
                    y_reg <= y_reg + x_shifted;
                    z_reg <= z_reg - atan_val;
                else
                    x_reg <= x_reg + y_shifted;
                    y_reg <= y_reg - x_shifted;
                    z_reg <= z_reg + atan_val;
                end if;

                if stage = ITERS - 1 then
                    active <= '0';
                    done   <= '1';
                else
                    stage <= stage + 1;
                end if;
            end if;
        end if;
    end process cordic_proc;

    x_out <= std_logic_vector(x_reg);
    y_out <= std_logic_vector(y_reg);
    z_out <= std_logic_vector(z_reg);
end RTL;
