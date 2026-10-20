library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

entity dip_adder is
    port(
        a   : in  std_logic_vector(3 downto 0);
        b   : in  std_logic_vector(3 downto 0);
        sum : out std_logic_vector(4 downto 0)
    );
end dip_adder;

architecture rtl of dip_adder is
begin
    sum <= std_logic_vector(resize(unsigned(a), 5) + resize(unsigned(b), 5));
end rtl;
