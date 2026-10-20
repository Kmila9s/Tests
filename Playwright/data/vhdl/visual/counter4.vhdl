library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

-- 4-bit counter: rising-edge clock, sync reset (active high),
-- exposes count(3 downto 0). Used by the LED-array visual tests
-- so each bit position can be observed lighting in sequence.
entity counter4 is
    port (
        clk   : in  std_logic;
        rst   : in  std_logic;
        count : out std_logic_vector(3 downto 0)
    );
end entity;

architecture behavioral of counter4 is
    signal r : unsigned(3 downto 0) := (others => '0');
begin
    process(clk)
    begin
        if rising_edge(clk) then
            if rst = '1' then
                r <= (others => '0');
            else
                r <= r + 1;
            end if;
        end if;
    end process;

    count <= std_logic_vector(r);
end architecture;
