library ieee;
use ieee.std_logic_1164.all;

entity struct_top is
  port (
    clk  : in  std_logic;
    x    : in  std_logic_vector(7 downto 0);
    y    : in  std_logic_vector(7 downto 0);
    outp : out std_logic_vector(7 downto 0)
  );
end entity struct_top;

architecture struct of struct_top is
  signal s : std_logic_vector(7 downto 0);
  component adder8 is
    port ( a : in std_logic_vector(7 downto 0); b : in std_logic_vector(7 downto 0); s : out std_logic_vector(7 downto 0) );
  end component;
  component reg8 is
    port ( clk : in std_logic; d : in std_logic_vector(7 downto 0); q : out std_logic_vector(7 downto 0) );
  end component;
begin
  u_add : adder8 port map ( a => x, b => y, s => s );
  u_reg : reg8  port map ( clk => clk, d => s, q => outp );
end architecture struct;
