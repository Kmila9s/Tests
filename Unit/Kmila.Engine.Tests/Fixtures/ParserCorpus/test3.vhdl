library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity MUX4to1 is
    Port (
        A,g,h : in STD_LOGIC;
        B : in STD_LOGIC;
        C : in STD_LOGIC;
        D : in STD_LOGIC;
        SEL : in STD_LOGIC_VECTOR(1 downto 0);
        Y : out STD_LOGIC
    );
end MUX4to1;
-- This is a comment
architecture Behavioral of MUX4to1 is
begin
    process (A, B, C, D, SEL)
    begin
        case SEL is
            when "00" => Y <= A;
            when "01" => Y <= B;
            when "10" => Y <= C;
            when "11" => Y <= D;
            when others => Y <= '0'; -- Default case
        end case;
    end process;
end Behavioral;
