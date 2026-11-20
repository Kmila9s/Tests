-- G74: VHDL identifiers are case-insensitive everywhere.
library ieee;
use ieee.std_logic_1164.all;

entity Mixed_Case is
  port (CLK, D : in std_logic; Q : out std_logic; N : out std_logic_vector(1 downto 0));
end entity;

architecture RTL of MIXED_case is
  signal Reg : std_logic := '0';
begin
  P_Reg: process (clk)
  begin
    if Rising_Edge(Clk) then
      REG <= d;
    end if;
  end process p_reg;
  q <= reg;
  Cnt: process (Clk)
    variable C : natural range 0 to 3 := 0;
  begin
    if rising_edge(CLK) then
      Outer: for I in 0 to 1 loop
        c := (C + 1) mod 4;
        exit OUTER when i = 0;
      end loop outer;
      n <= "00" when C = 0 else "01" when c = 1 else "10" when C = 2 else "11";
    end if;
  end process CNT;
end architecture rtl;
