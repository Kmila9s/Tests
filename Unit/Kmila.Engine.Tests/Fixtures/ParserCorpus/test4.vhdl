library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_ARITH.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

entity ALU_8bit is
    Port ( A : in STD_LOGIC_VECTOR (7 downto 0);
           B : in STD_LOGIC_VECTOR (7 downto 0);
           Cin : in STD_LOGIC;
           Opcode : in STD_LOGIC_VECTOR (3 downto 0);
           Result : out STD_LOGIC_VECTOR (7 downto 0);
           Cout : out STD_LOGIC);
end ALU_8bit;

architecture Behavioral of ALU_8bit is
    signal temp, temp2 : STD_LOGIC_VECTOR(7 downto 0);
    signal temp3 : std_logic;
    signal temp4: boolean := true;
    signal temp5: integer := 123;
    constant t6 : bit_vector(3 downto 0) := "1010";
    signal data   : std_logic_vector(7 downto 0) := "00000000";
    signal x, y, z : integer := 10;
    signal state : integer range 0 to 7;
    signal flag  : bit;
    constant t1 : natural := 42;
    signal word  : bit_vector(15 downto 0);
    signal ready : std_logic := '1';
    constant t3 : time := 5 ns;
    signal tri_state : std_logic := 'Z';
    signal error : std_logic := 'X';
    constant t8 : std_logic_vector(7 downto 0) := x"FF";
    signal clk_delayed : std_logic := '0' after 10 ns;
    constant t : integer := 10;
    constant t2 : real := 3.14;
    
    constant t4 : boolean := true;
    constant t5 : bit := '0';
    
    constant t7 : std_logic := '1';
    
    constant t9 : character := 'A';
    constant t0 : string := "Hello";
    
    constant t12 : positive := 7;
begin
    process (A, B, Cin, Opcode)
    begin
        case Opcode is
            when "0000" => -- ADD
                temp <= B after 200 ms;
                temp <= A + B + Cin;
                Cout <= (A(7) and B(7)) or (A(7) and Cin) or (B(7) and Cin);
                Result <= temp;
            when "0001" => -- SUBTRACT
                temp <= A - B - Cin;
                Cout <= (not A(7) and B(7)) or (not A(7) and Cin) or (B(7) and Cin);
                Result <= temp;
            when others => -- INVALID OPERATION
                Result <= (others => '0');
                Cout <= '0';
        end case;
    end process;
end Behavioral;
