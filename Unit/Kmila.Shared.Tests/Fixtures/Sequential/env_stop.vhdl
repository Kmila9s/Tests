-- G31: std.env.stop ends the simulation early (self-clocked, no inputs).
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity env_stop is
  port (q : out std_logic_vector(3 downto 0));
end entity;
architecture sim of env_stop is
  signal clk : std_logic := '0';
  signal c : unsigned(3 downto 0) := (others => '0');
begin
  clk <= not clk after 5 ns;
  process (clk)
  begin
    if rising_edge(clk) then
      c <= c + 1;
      if c = 4 then
        std.env.stop;
      end if;
    end if;
  end process;
  q <= std_logic_vector(c);
end architecture;
