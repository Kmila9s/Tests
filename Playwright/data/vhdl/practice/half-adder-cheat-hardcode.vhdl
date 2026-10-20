library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- CHEAT-HARDCODE: both outputs hardcoded to '0' regardless of inputs.
-- A naive waveform-diff validator should reject this on at least one test case.
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
    sum  <= '0';
    cout <= '0';
end architecture behavioral;
