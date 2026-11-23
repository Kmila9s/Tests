library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

-- Regression fence (2026-10-11): a combinational 7-segment decoder whose case
-- selector is the EXPRESSION `to_integer(unsigned(val))` rather than a plain
-- signal. Before the CaseStructure fix an expression selector left the whole
-- case a no-op (not even `when others` fired), so `seg` stayed stuck. Driven
-- combinationally so GHDL and Kmila see identical inputs with no clock/reset
-- edge races — any mismatch is a real selector-evaluation bug.
entity case_expr_selector is
    port (
        val : in  std_logic_vector(3 downto 0);
        seg : out std_logic_vector(6 downto 0)
    );
end case_expr_selector;

architecture rtl of case_expr_selector is
begin
    process(val)
    begin
        case to_integer(unsigned(val)) is
            when 0 => seg <= "0000001";
            when 1 => seg <= "1001111";
            when 2 => seg <= "0010010";
            when 3 => seg <= "0000110";
            when 4 => seg <= "1001100";
            when 5 => seg <= "0100100";
            when 6 => seg <= "0100000";
            when 7 => seg <= "0001111";
            when 8 => seg <= "0000000";
            when 9 => seg <= "0000100";
            when others => seg <= "1111111";
        end case;
    end process;
end rtl;
