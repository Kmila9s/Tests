library IEEE;
use IEEE.std_logic_1164.all;

entity passthrough is
    port (
        btn : in  std_logic;
        led : out std_logic
    );
end entity;

architecture behavioral of passthrough is
begin
    led <= btn;
end architecture;
