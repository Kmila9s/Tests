library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- WRONG: swaps the gates (sum should be xor, cout should be and).
entity half_adder is
    port (
        a   : in  STD_LOGIC;
        b   : in  STD_LOGIC;
        sum : out STD_LOGIC;
        cout: out STD_LOGIC
    );
end entity half_adder;

architecture behavioral of half_adder is
begin
    sum  <= a and b;
    cout <= a or  b;
end architecture behavioral;
