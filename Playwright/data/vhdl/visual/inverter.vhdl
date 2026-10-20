library IEEE;
use IEEE.std_logic_1164.all;

entity inverter is
    port (
        btn : in  std_logic;
        led : out std_logic
    );
end entity;

architecture behavioral of inverter is
begin
    led <= not btn;
end architecture;
