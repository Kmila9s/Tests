-- Test 36: 8-bit ALU with Z/N/C/V flags (4-bit op-code)
-- Curriculum: Arquitectura de Computadoras

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity ALU_8bit is
    port (
        a, b   : in  std_logic_vector(7 downto 0);
        op     : in  std_logic_vector(3 downto 0);
        c_in   : in  std_logic;
        result : out std_logic_vector(7 downto 0);
        zero   : out std_logic;
        neg    : out std_logic;
        carry  : out std_logic;
        ovf    : out std_logic
    );
end ALU_8bit;

architecture RTL of ALU_8bit is
    constant OP_ADD : std_logic_vector(3 downto 0) := "0000";
    constant OP_SUB : std_logic_vector(3 downto 0) := "0001";
    constant OP_AND : std_logic_vector(3 downto 0) := "0010";
    constant OP_OR  : std_logic_vector(3 downto 0) := "0011";
    constant OP_XOR : std_logic_vector(3 downto 0) := "0100";
    constant OP_NOT : std_logic_vector(3 downto 0) := "0101";
    constant OP_SHL : std_logic_vector(3 downto 0) := "0110";
    constant OP_SHR : std_logic_vector(3 downto 0) := "0111";
    constant OP_INC : std_logic_vector(3 downto 0) := "1000";
    constant OP_DEC : std_logic_vector(3 downto 0) := "1001";

    signal res9 : unsigned(8 downto 0);
    signal res  : std_logic_vector(7 downto 0);
begin
    alu_proc : process(a, b, op, c_in)
    begin
        case op is
            when OP_ADD =>
                res9 <= ('0' & unsigned(a)) + ('0' & unsigned(b)) + ("00000000" & c_in);
            when OP_SUB =>
                res9 <= ('0' & unsigned(a)) - ('0' & unsigned(b));
            when OP_AND =>
                res9 <= '0' & unsigned(a and b);
            when OP_OR =>
                res9 <= '0' & unsigned(a or b);
            when OP_XOR =>
                res9 <= '0' & unsigned(a xor b);
            when OP_NOT =>
                res9 <= '0' & unsigned(not a);
            when OP_SHL =>
                res9 <= '0' & unsigned(a(6 downto 0) & '0');
            when OP_SHR =>
                res9 <= '0' & unsigned('0' & a(7 downto 1));
            when OP_INC =>
                res9 <= ('0' & unsigned(a)) + 1;
            when OP_DEC =>
                res9 <= ('0' & unsigned(a)) - 1;
            when others =>
                res9 <= (others => '0');
        end case;
    end process alu_proc;

    res    <= std_logic_vector(res9(7 downto 0));
    result <= res;
    zero   <= '1' when res = "00000000" else '0';
    neg    <= res(7);
    carry  <= res9(8);
    ovf    <= (a(7) xor res(7)) and (b(7) xor res(7));
end RTL;
