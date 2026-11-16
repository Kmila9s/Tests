library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_ARITH.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

package Math_Utils is
    function Multiply_Unsigned(
        A : STD_LOGIC_VECTOR;
        B : STD_LOGIC_VECTOR
    ) return STD_LOGIC_VECTOR;
end Math_Utils;

package body Math_Utils is
    function Multiply_Unsigned(
        A : STD_LOGIC_VECTOR;
        B : STD_LOGIC_VECTOR
    ) return STD_LOGIC_VECTOR is
            STD_LOGIC_VECTOR((A'length + B'length) - 1 downto 0);
    begin
        Result := (others => '0');
        for i in A'range loop
            if A(i) = '1' then
                Result := Result + (B & (others => '0') after A'length - i - 1);
            end if;
        end loop;
        return Result;
    end Multiply_Unsigned;
end Math_Utils;

entity DSP_Block is
    generic (
        DATA_WIDTH : integer := 16;
        PIPELINE_DEPTH : integer := 2
    );
    port (
        CLK     : in  STD_LOGIC;
        RESET   : in  STD_LOGIC;
        A       : in  STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0);
        B       : in  STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0);
        RESULT  : out STD_LOGIC_VECTOR((2 * DATA_WIDTH) - 1 downto 0)
    );
end DSP_Block;

architecture Pipeline of DSP_Block is
    signal Reg_A      : STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0) := (others => '0');
    signal Reg_B      : STD_LOGIC_VECTOR(DATA_WIDTH - 1 downto 0) := (others => '0');
    signal Reg_Result : STD_LOGIC_VECTOR((2 * DATA_WIDTH) - 1 downto 0) := (others => '0');
    signal Pipe_Stages : STD_LOGIC_VECTOR((PIPELINE_DEPTH * DATA_WIDTH) - 1 downto 0);
begin
    process (CLK, RESET)
    begin
        if RESET = '1' then
            Reg_A      <= (others => '0');
            Reg_B      <= (others => '0');
            Reg_Result <= (others => '0');
            Pipe_Stages <= (others => '0');
        elsif rising_edge(CLK) then
            Reg_A <= A;
            Reg_B <= B;
            Pipe_Stages <= Pipe_Stages(DATA_WIDTH - 1 downto 0) & A;
            Reg_Result <= Multiply_Unsigned(Reg_A, Reg_B);
        end if;
    end process;

    RESULT <= Reg_Result;
end Pipeline;

configuration DSP_Config of DSP_Block is
    for Pipeline
    end for;
end DSP_Config;
