library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity half_adder is
    port (
        a, b: in STD_LOGIC;
        sum, cout: out STD_LOGIC
    );
end entity;

architecture behavioral of half_adder is
begin
    sum <= a xor b;
    cout <= a and b;
end architecture;
