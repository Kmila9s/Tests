library ieee;
use ieee.std_logic_1164.all;

entity reg8 is
  port (
    clk : in  std_logic;
    d   : in  std_logic_vector(7 downto 0);
    q   : out std_logic_vector(7 downto 0)
  );
end entity reg8;

architecture rtl of reg8 is
begin
  process(clk)
  begin
    if rising_edge(clk) then
      q <= d;
    end if;
  end process;
end architecture rtl;
