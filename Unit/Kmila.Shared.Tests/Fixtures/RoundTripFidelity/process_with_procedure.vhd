library ieee;
use ieee.std_logic_1164.all;

-- Phase 3 roundtrip follow-up (2026-07-23) — procedure declared inside
-- a process's declarative region. Pre-fix, VhdlBlockBuilder.ProcessRe's
-- non-greedy `.*?\bbegin\b` split at the procedure body's `begin`,
-- leaving the procedure header as raw_decl and the closer
-- `end procedure;` as two raw_seq tiles. Fix: SplitProcessDeclsBody
-- walks the region with subprogram-depth tracking to find the correct
-- depth-0 begin. Mirrors SimulatorBench case 144-procedure-wait.
entity process_with_procedure is
    port (
        q : out std_logic_vector(3 downto 0)
    );
end process_with_procedure;

architecture rtl of process_with_procedure is
    signal state : std_logic := '0';
begin
    q <= "1010" when state = '1' else "0101";

    process
        procedure toggle_after (signal s : inout std_logic) is
        begin
            wait for 20 ns;
            if s = '0' then
                s <= '1';
            else
                s <= '0';
            end if;
        end procedure;
    begin
        for i in 0 to 5 loop
            toggle_after(state);
        end loop;
        wait;
    end process;
end rtl;
