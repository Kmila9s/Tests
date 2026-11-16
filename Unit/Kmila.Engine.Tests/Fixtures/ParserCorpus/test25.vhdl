-- Test 25: BCD Up/Down Counter (single decade) with carry-out
-- Curriculum: Diseño de Sistemas Digitales, Unit 3.4

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity BCDCounter is
    port (
        clk     : in  std_logic;
        rst     : in  std_logic;
        en      : in  std_logic;
        up_down : in  std_logic;
        digit   : out std_logic_vector(3 downto 0);
        carry   : out std_logic
    );
end BCDCounter;

architecture RTL of BCDCounter is
    signal cnt    : unsigned(3 downto 0) := (others => '0');
    signal carry_int : std_logic := '0';
begin
    bcd_proc : process(clk, rst)
    begin
        if rst = '1' then
            cnt <= (others => '0');
            carry_int <= '0';
        elsif rising_edge(clk) then
            carry_int <= '0';
            if en = '1' then
                if up_down = '1' then
                    if cnt = 9 then
                        cnt <= (others => '0');
                        carry_int <= '1';
                    else
                        cnt <= cnt + 1;
                    end if;
                else
                    if cnt = 0 then
                        cnt <= to_unsigned(9, 4);
                        carry_int <= '1';
                    else
                        cnt <= cnt - 1;
                    end if;
                end if;
            end if;
        end if;
    end process bcd_proc;

    digit <= std_logic_vector(cnt);
    carry <= carry_int;
end RTL;
