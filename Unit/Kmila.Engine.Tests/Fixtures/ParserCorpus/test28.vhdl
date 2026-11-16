-- Test 28: 8x8 Unsigned Combinational Multiplier (shift-and-add)
-- Curriculum: Fundamentos de Diseño Digital, multipliers

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity Multiplier8x8 is
    port (
        a       : in  std_logic_vector(7 downto 0);
        b       : in  std_logic_vector(7 downto 0);
        product : out std_logic_vector(15 downto 0)
    );
end Multiplier8x8;

architecture RTL of Multiplier8x8 is
    signal partial : std_logic_vector(15 downto 0);
begin
    mul_proc : process(a, b)
        variable acc : unsigned(15 downto 0);
    begin
        acc := (others => '0');
        for i in 0 to 7 loop
            if b(i) = '1' then
                acc := acc + (resize(unsigned(a), 16) sll i);
            end if;
        end loop;
        partial <= std_logic_vector(acc);
    end process mul_proc;

    product <= partial;
end RTL;
