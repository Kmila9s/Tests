library IEEE;
use IEEE.std_logic_1164.all;

-- Switch widget proves out against this: switch's `out` pin drives
-- `sw`, LED widget reads `lamp`. Same passthrough body as the
-- passthrough fixture but named so the test artefacts don't collide.
entity toggle_led is
    port (
        sw   : in  std_logic;
        lamp : out std_logic
    );
end entity;

architecture behavioral of toggle_led is
begin
    lamp <= sw;
end architecture;
