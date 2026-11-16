-- Test 26: 16-to-4 Priority Encoder with Valid output
-- Curriculum: Fundamentos de Diseño Digital, combinational

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity PriorityEncoder16 is
    port (
        d_in  : in  std_logic_vector(15 downto 0);
        addr  : out std_logic_vector(3 downto 0);
        valid : out std_logic
    );
end PriorityEncoder16;

architecture RTL of PriorityEncoder16 is
begin
    enc_proc : process(d_in)
        variable found : std_logic;
        variable idx   : integer;
    begin
        found := '0';
        idx := 0;
        for i in 15 downto 0 loop
            if d_in(i) = '1' and found = '0' then
                idx := i;
                found := '1';
            end if;
        end loop;
        addr  <= std_logic_vector(to_unsigned(idx, 4));
        valid <= found;
    end process enc_proc;
end RTL;
