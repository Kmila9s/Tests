library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

-- Long-run memory stress fixture.
--
-- A free-running clocked counter whose internal signals change on every clock
-- edge. The counter never stops and never reaches a steady state, so a long
-- simulation produces an unbounded stream of per-edge transitions: exactly the
-- shape that makes SignalHistory / WaveformData / the live-replay doc grow
-- without bound in the UI run path (the long-running freeze + crash repro).
--
-- Several derived signals (s0..s7 = low counter bits) inflate the number of
-- tracked signals so the per-frame "full signal-table snapshot" in the replay
-- path is clearly larger than the per-edge delta (usually 1-3 signals). That
-- gap is what the delta-snapshot fix must close.
entity stress_counter is
    port(
        clk : in  std_logic;
        q   : out std_logic_vector(15 downto 0)
    );
end stress_counter;

architecture rtl of stress_counter is
    signal cnt : unsigned(15 downto 0) := (others => '0');
    signal s0, s1, s2, s3, s4, s5, s6, s7 : std_logic := '0';
begin
    process(clk)
    begin
        if rising_edge(clk) then
            cnt <= cnt + 1;
        end if;
    end process;

    s0 <= cnt(0);
    s1 <= cnt(1);
    s2 <= cnt(2);
    s3 <= cnt(3);
    s4 <= cnt(4);
    s5 <= cnt(5);
    s6 <= cnt(6);
    s7 <= cnt(7);
    q  <= std_logic_vector(cnt);
end rtl;
