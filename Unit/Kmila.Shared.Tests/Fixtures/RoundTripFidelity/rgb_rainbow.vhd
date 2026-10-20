library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity rgb_rainbow is
    port(
        clk     : in  std_logic;
        reset   : in  std_logic;
        step_en : in  std_logic;
        red     : out std_logic_vector(8 downto 0);
        green   : out std_logic_vector(8 downto 0);
        blue    : out std_logic_vector(8 downto 0)
    );
end rgb_rainbow;

architecture rtl of rgb_rainbow is
    -- Three 9-bit Galois-style LFSRs, one per colour plane. Each uses
    -- a different tap bit (4/3/5) so the planes don't lock-step, giving
    -- a rich colour cycle across the matrix without any dedicated
    -- colour-generator arithmetic. The shift is written inline (not as
    -- a VHDL function) because the current interpreter's user-defined
    -- functions aren't reactive across process re-fires (2026-07-05
    -- gap — see project_visual_runtime_mode memory).
    signal r_reg : std_logic_vector(8 downto 0) := "101101010";
    signal g_reg : std_logic_vector(8 downto 0) := "011010110";
    signal b_reg : std_logic_vector(8 downto 0) := "110100101";
begin
    process(clk, reset)
    begin
        if reset = '1' then
            r_reg <= "101101010";
            g_reg <= "011010110";
            b_reg <= "110100101";
        elsif rising_edge(clk) then
            if step_en = '1' then
                r_reg <= (r_reg(0) xor r_reg(4)) & r_reg(8 downto 1);
                g_reg <= (g_reg(0) xor g_reg(3)) & g_reg(8 downto 1);
                b_reg <= (b_reg(0) xor b_reg(5)) & b_reg(8 downto 1);
            end if;
        end if;
    end process;

    red   <= r_reg;
    green <= g_reg;
    blue  <= b_reg;
end rtl;
