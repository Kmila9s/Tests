-- Test 32: Traffic Light Mealy FSM (NS/EW intersection, four states)
-- Curriculum: Diseño de Sistemas Digitales, Unit 2.2

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity TrafficLight is
    port (
        clk      : in  std_logic;
        rst      : in  std_logic;
        sensor   : in  std_logic;
        ns_light : out std_logic_vector(1 downto 0);
        ew_light : out std_logic_vector(1 downto 0)
    );
end TrafficLight;

architecture RTL of TrafficLight is
    constant S_NS_GREEN  : std_logic_vector(1 downto 0) := "00";
    constant S_NS_YELLOW : std_logic_vector(1 downto 0) := "01";
    constant S_EW_GREEN  : std_logic_vector(1 downto 0) := "10";
    constant S_EW_YELLOW : std_logic_vector(1 downto 0) := "11";

    signal state : std_logic_vector(1 downto 0) := S_NS_GREEN;
    signal timer : unsigned(7 downto 0) := (others => '0');
begin
    fsm_proc : process(clk, rst)
    begin
        if rst = '1' then
            state <= S_NS_GREEN;
            timer <= (others => '0');
        elsif rising_edge(clk) then
            timer <= timer + 1;
            case state is
                when S_NS_GREEN =>
                    if timer = 100 then
                        state <= S_NS_YELLOW;
                        timer <= (others => '0');
                    end if;
                when S_NS_YELLOW =>
                    if timer = 30 then
                        state <= S_EW_GREEN;
                        timer <= (others => '0');
                    end if;
                when S_EW_GREEN =>
                    if timer = 100 and sensor = '1' then
                        state <= S_EW_YELLOW;
                        timer <= (others => '0');
                    end if;
                when S_EW_YELLOW =>
                    if timer = 30 then
                        state <= S_NS_GREEN;
                        timer <= (others => '0');
                    end if;
                when others =>
                    state <= S_NS_GREEN;
            end case;
        end if;
    end process fsm_proc;

    out_proc : process(state)
    begin
        case state is
            when S_NS_GREEN =>
                ns_light <= "10"; ew_light <= "00";
            when S_NS_YELLOW =>
                ns_light <= "01"; ew_light <= "00";
            when S_EW_GREEN =>
                ns_light <= "00"; ew_light <= "10";
            when S_EW_YELLOW =>
                ns_light <= "00"; ew_light <= "01";
            when others =>
                ns_light <= "00"; ew_light <= "00";
        end case;
    end process out_proc;
end RTL;
