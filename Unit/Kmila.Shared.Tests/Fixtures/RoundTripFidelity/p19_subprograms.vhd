library ieee;
use ieee.std_logic_1164.all;
entity p19_tb is
end entity;
architecture sim of p19_tb is
    component and_gate is
        port (
            a : in std_logic;
            b : in std_logic;
            y : out std_logic
        );
    end component;
    signal a, b, y : std_logic;
    procedure check_and_gate(
        a_in : in std_logic;
        b_in : in std_logic;
        expected_y : in std_logic
    ) is
    begin
        wait for 1 ns;
        assert (y = expected_y) report "ERROR" severity failure;
    end procedure;
    function bit_and(x : in std_logic; y : in std_logic) return std_logic is
        variable r : std_logic := '0';
    begin
        return r;
    end function;
begin
    u1: and_gate port map (a => a, b => b, y => y);
    stim: process
    begin
        check_and_gate('0', '0', '0');
        check_and_gate('1', '1', '1');
        wait;
    end process;
end architecture;
