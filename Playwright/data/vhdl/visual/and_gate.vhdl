library IEEE;
use IEEE.std_logic_1164.all;

entity and_gate is
    port (
        btn_a : in  std_logic;
        btn_b : in  std_logic;
        led   : out std_logic
    );
end entity;

architecture behavioral of and_gate is
begin
    led <= btn_a and btn_b;
end architecture;
