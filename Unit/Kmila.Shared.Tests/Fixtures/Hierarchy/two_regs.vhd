library ieee;
use ieee.std_logic_1164.all;

entity two_regs is
  port (
    clk : in  std_logic;
    d   : in  std_logic_vector(7 downto 0);
    q1  : out std_logic_vector(7 downto 0);
    q2  : out std_logic_vector(7 downto 0)
  );
end entity two_regs;

architecture struct of two_regs is
  component reg8 is
    port ( clk : in std_logic; d : in std_logic_vector(7 downto 0); q : out std_logic_vector(7 downto 0) );
  end component;
begin
  u1 : reg8 port map ( clk => clk, d => d, q => q1 );
  u2 : reg8 port map ( clk => clk, d => d, q => q2 );
end architecture struct;
