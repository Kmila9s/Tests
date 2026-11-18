-- G27: end label that does not match the statement label (GHDL rejects).
library ieee; use ieee.std_logic_1164.all;
entity label_bad is
  port (a : in std_logic; y : out std_logic);
end entity;
architecture rtl of label_bad is
begin
  process (a)
  begin
    chk: if a = '1' then
      y <= '1';
    else
      y <= '0';
    end if other;
  end process;
end architecture;
