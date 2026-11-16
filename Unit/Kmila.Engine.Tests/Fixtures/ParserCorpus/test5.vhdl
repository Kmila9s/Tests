library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_ARITH.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

entity ALU is
    generic (
        DATA_WIDTH : integer := 8
    );
    port (
        A         : in  STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0);
        B         : in  STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0);
        Opcode    : in  STD_LOGIC_VECTOR(2 downto 0);
        Result    : out STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0);
        Zero_Flag : out STD_LOGIC
    );
end ALU;

architecture Behavioral of ALU is
    signal Temp_Result : STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0);
begin

    process (A, B, Opcode)
    begin
        case Opcode is
            when "000" => -- Addition
                Temp_Result <= A + B;
            when "001" => -- Subtraction
                Temp_Result <= A - B;
            when "010" => -- AND operation
                Temp_Result <= A and B;
            when "011" => -- OR operation
                Temp_Result <= A or B;
            when "100" => -- XOR operation
                Temp_Result <= A xor B;
            when others => -- Default case
                Temp_Result <= (others => '0');
        end case;

        -- Set the zero flag if result is zero
        if Temp_Result = (others => '0') then
            Zero_Flag <= '1';
        else
            Zero_Flag <= '0';
        end if;
    end process;

    -- Assign the result signal
    Result <= Temp_Result;

end Behavioral;
