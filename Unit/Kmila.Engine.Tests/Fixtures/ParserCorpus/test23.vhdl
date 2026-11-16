-- Test 23: 8-bit Barrel Shifter (rotate left, rotate right, arithmetic shift)
-- Curriculum: Diseño de Sistemas Digitales, Unit 1.1.5

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity BarrelShifter is
    port (
        d_in   : in  std_logic_vector(7 downto 0);
        amount : in  std_logic_vector(2 downto 0);
        op     : in  std_logic_vector(1 downto 0);
        d_out  : out std_logic_vector(7 downto 0)
    );
end BarrelShifter;

architecture RTL of BarrelShifter is
    signal stage0 : std_logic_vector(7 downto 0);
    signal stage1 : std_logic_vector(7 downto 0);
    signal stage2 : std_logic_vector(7 downto 0);
    constant OP_ROL : std_logic_vector(1 downto 0) := "00";
    constant OP_ROR : std_logic_vector(1 downto 0) := "01";
    constant OP_SHL : std_logic_vector(1 downto 0) := "10";
    constant OP_SHR : std_logic_vector(1 downto 0) := "11";
begin
    shift_proc : process(d_in, amount, op)
    begin
        case op is
            when OP_ROL =>
                stage0 <= d_in;
                stage1 <= stage0;
                stage2 <= stage1;
                d_out  <= stage2;
            when OP_ROR =>
                stage0 <= d_in;
                stage1 <= stage0;
                stage2 <= stage1;
                d_out  <= stage2;
            when OP_SHL =>
                d_out <= d_in(6 downto 0) & '0';
            when OP_SHR =>
                d_out <= '0' & d_in(7 downto 1);
            when others =>
                d_out <= (others => '0');
        end case;
    end process shift_proc;
end RTL;
