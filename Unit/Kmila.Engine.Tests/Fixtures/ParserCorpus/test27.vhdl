-- Test 27: 16-bit Carry-Lookahead Adder (4-bit groups)
-- Curriculum: Fundamentos de Diseño Digital, arithmetic units

library IEEE;
use IEEE.std_logic_1164.all;

entity CLAAdder16 is
    port (
        a, b  : in  std_logic_vector(15 downto 0);
        c_in  : in  std_logic;
        sum   : out std_logic_vector(15 downto 0);
        c_out : out std_logic
    );
end CLAAdder16;

architecture RTL of CLAAdder16 is
    signal g, p : std_logic_vector(15 downto 0);
    signal c    : std_logic_vector(16 downto 0);
begin
    -- Generate and propagate
    gp_proc : process(a, b)
    begin
        for i in 0 to 15 loop
            g(i) <= a(i) and b(i);
            p(i) <= a(i) xor b(i);
        end loop;
    end process gp_proc;

    -- Carry chain
    c(0) <= c_in;
    carry_proc : process(g, p, c)
    begin
        for i in 0 to 15 loop
            c(i + 1) <= g(i) or (p(i) and c(i));
        end loop;
    end process carry_proc;

    -- Sum
    sum_proc : process(p, c)
    begin
        for i in 0 to 15 loop
            sum(i) <= p(i) xor c(i);
        end loop;
    end process sum_proc;

    c_out <= c(16);
end RTL;
