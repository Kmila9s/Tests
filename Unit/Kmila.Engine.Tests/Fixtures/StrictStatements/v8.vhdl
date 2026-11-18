library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;
entity counter4 is
    port ( clk : in STD_LOGIC; reset : in STD_LOGIC; q : out STD_LOGIC_VECTOR(3 downto 0));
end entity counter4;
architecture behavioral of counter4 is
    signal count : unsigned(3 downto 0) := (others => '0');
begin
    process(clk, reset)
    begin
        
        if reset = '1' then
            count <= (others => '0');
        elsif rising_edge(clk) then
            count <= count + 1; 
        end if;
    end process;
    by one
    q <= std_logic_vector(count);
end architecture behavioral;
