library ieee;
use ieee.std_logic_1164.all;

entity tb_constructs is
end entity;

architecture sim of tb_constructs is
  signal a, s : std_logic_vector(1 downto 0);
  signal y_init, y_cv, y_sv : std_logic_vector(3 downto 0);
  signal y_agg : std_logic_vector(2 downto 0);
  signal y_bind, y_gen, y_case : std_logic_vector(1 downto 0);
  signal z_bind : std_logic;
begin
  dut: entity work.constructs port map (a => a, s => s, y_init => y_init, y_cv => y_cv, y_sv => y_sv,
    y_agg => y_agg, y_bind => y_bind, z_bind => z_bind, y_gen => y_gen, y_case => y_case);
  stim: process
  begin
    a <= "01"; s <= "00"; wait for 10 ns;
    a <= "10"; s <= "01"; wait for 10 ns;
    a <= "11"; s <= "10"; wait for 10 ns;
    a <= "00"; s <= "11"; wait;
  end process;
  mon: process
  begin
    wait for 5 ns;
    for k in 0 to 3 loop
      report "t=" & integer'image(k * 10 + 5)
        & " co.y_init=" & to_string(y_init) & " co.y_cv=" & to_string(y_cv)
        & " co.y_sv=" & to_string(y_sv) & " co.y_agg=" & to_string(y_agg)
        & " co.y_bind=" & to_string(y_bind) & " co.z_bind=" & std_logic'image(z_bind)(2)
        & " co.y_gen=" & to_string(y_gen) & " co.y_case=" & to_string(y_case);
      wait for 10 ns;
    end loop;
    wait;
  end process;
end architecture;
