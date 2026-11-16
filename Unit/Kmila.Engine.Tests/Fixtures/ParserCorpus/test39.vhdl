-- Test 39: 32-bit Timer with Input Capture and Compare-Match
-- Curriculum: Sistemas en Chip, Unit 2.3

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity TimerCapture is
    port (
        clk        : in  std_logic;
        rst        : in  std_logic;
        capture_in : in  std_logic;
        compare    : in  std_logic_vector(31 downto 0);
        en         : in  std_logic;
        clr        : in  std_logic;
        counter    : out std_logic_vector(31 downto 0);
        capture    : out std_logic_vector(31 downto 0);
        cmp_match  : out std_logic;
        ovf        : out std_logic
    );
end TimerCapture;

architecture RTL of TimerCapture is
    signal cnt        : unsigned(31 downto 0) := (others => '0');
    signal cap_reg    : std_logic_vector(31 downto 0) := (others => '0');
    signal cap_d1     : std_logic := '0';
    signal cap_rise   : std_logic := '0';
    signal ovf_int    : std_logic := '0';
    signal match_int  : std_logic := '0';
begin
    cap_rise <= capture_in and (not cap_d1);

    timer_proc : process(clk, rst)
    begin
        if rst = '1' then
            cnt       <= (others => '0');
            cap_reg   <= (others => '0');
            cap_d1    <= '0';
            ovf_int   <= '0';
            match_int <= '0';
        elsif rising_edge(clk) then
            cap_d1    <= capture_in;
            ovf_int   <= '0';
            match_int <= '0';

            if clr = '1' then
                cnt <= (others => '0');
            elsif en = '1' then
                if cnt = x"FFFFFFFF" then
                    cnt     <= (others => '0');
                    ovf_int <= '1';
                else
                    cnt <= cnt + 1;
                end if;
                if std_logic_vector(cnt) = compare then
                    match_int <= '1';
                end if;
            end if;

            if cap_rise = '1' then
                cap_reg <= std_logic_vector(cnt);
            end if;
        end if;
    end process timer_proc;

    counter   <= std_logic_vector(cnt);
    capture   <= cap_reg;
    cmp_match <= match_int;
    ovf       <= ovf_int;
end RTL;
