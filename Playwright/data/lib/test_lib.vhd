-- Tiny VHDL library file for Playwright import tests.
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

package kmila_test_lib is
    function my_inv(s : STD_LOGIC) return STD_LOGIC;
end package;

package body kmila_test_lib is
    function my_inv(s : STD_LOGIC) return STD_LOGIC is
    begin
        return not s;
    end function;
end package body;
