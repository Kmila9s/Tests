-- G13: integer signal in the VCD (GHDL: $var integer 32).
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity int_vcd is
  port (clk : in std_logic; y : out std_logic_vector(3 downto 0));
end entity;
architecture rtl of int_vcd is
  signal cnt : integer range 0 to 15 := 0;
begin
  process (clk)
  begin
    if rising_edge(clk) then
      if cnt = 15 then cnt <= 0; else cnt <= cnt + 1; end if;
    end if;
  end process;
  y <= std_logic_vector(to_unsigned(cnt, 4));
end architecture;
