-- test_delta_swap.vhdl
-- Tests VHDL delta cycle semantics for signal swapping
--
-- This test verifies that signal assignments within the same process
-- use the OLD values of signals (as per VHDL semantics), not the
-- values being assigned in the same delta cycle.
--
-- Expected behavior:
--   Initial: a = '0', b = '1'
--   After process executes:
--     a <= b uses OLD value of b ('1'), so a becomes '1'
--     b <= a uses OLD value of a ('0'), so b becomes '0'
--   Result: a = '1', b = '0' (proper swap)
--
-- WRONG behavior (sequential/immediate assignment):
--   a <= b  -> a becomes '1' immediately
--   b <= a  -> b becomes '1' (uses NEW a!)
--   Result: a = '1', b = '1' (not a swap!)

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity test_delta_swap is
    Port (
        clk    : in  STD_LOGIC;
        swap   : in  STD_LOGIC;
        a_out  : out STD_LOGIC;
        b_out  : out STD_LOGIC
    );
end test_delta_swap;

architecture Behavioral of test_delta_swap is
    signal a : STD_LOGIC := '0';
    signal b : STD_LOGIC := '1';
begin
    -- Concurrent outputs
    a_out <= a;
    b_out <= b;

    -- Swap process: when swap='1' on rising edge of clk, swap a and b
    process(clk)
    begin
        if rising_edge(clk) then
            if swap = '1' then
                -- These two assignments should use OLD values of a and b
                -- Result should be a proper swap
                a <= b;  -- a gets OLD b = '1'
                b <= a;  -- b gets OLD a = '0'
            end if;
        end if;
    end process;

end Behavioral;
