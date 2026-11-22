-- test_combinational.vhdl
-- Tests combinational logic gates (AND, OR, XOR, NOT)

library IEEE;
use IEEE.std_logic_1164.all;

entity test_combinational is
    port (
        a      : in  std_logic;
        b      : in  std_logic;
        y_and  : out std_logic;
        y_or   : out std_logic;
        y_xor  : out std_logic;
        y_not  : out std_logic
    );
end test_combinational;

architecture Behavioral of test_combinational is
begin
    -- Combinational logic outputs
    y_and <= a and b;
    y_or  <= a or b;
    y_xor <= a xor b;
    y_not <= not a;
end Behavioral;
