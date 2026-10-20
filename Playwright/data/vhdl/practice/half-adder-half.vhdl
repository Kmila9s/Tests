library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- HALF-RIGHT: sum is correct, cout is hardcoded to '0' (fails when a=b=1).
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
    sum  <= a xor b;
    cout <= '0';
end architecture behavioral;
