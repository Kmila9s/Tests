library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity conv3 is end entity;
architecture t of conv3 is
  signal en : std_logic := '0';
  signal u : unsigned(3 downto 0);
  signal v : std_logic_vector(7 downto 0);
  signal p0, p1, p2, p3 : std_logic;
begin
  c: entity work.child port map (en => en, unsigned(b) => std_logic_vector(u));
  process begin
    wait for 1 ns; en <= '1'; wait for 1 ns;
    report "u=" & to_string(u) & " v=" & to_string(v) & " p=" & std_logic'image(p3) & std_logic'image(p0);
    wait;
  end process;
end architecture;
