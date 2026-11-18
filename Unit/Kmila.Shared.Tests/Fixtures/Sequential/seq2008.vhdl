-- G32: VHDL-2008 conditional and selected signal assignments inside a process.
library ieee; use ieee.std_logic_1164.all;
entity seq2008 is
  port (a, b : in std_logic; sel : in std_logic_vector(1 downto 0); y, z, w : out std_logic);
end entity;
architecture rtl of seq2008 is
begin
  process (a, b, sel)
  begin
    y <= a when sel = "00" else b when sel = "01" else '1';
    with sel select
      z <= a when "00", b when "01", '0' when others;
    if a = '1' then
      w <= b when sel(0) = '1' else '0';
    else
      w <= '1';
    end if;
  end process;
end architecture;
