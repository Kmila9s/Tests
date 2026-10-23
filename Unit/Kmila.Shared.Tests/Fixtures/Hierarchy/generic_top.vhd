library ieee;
use ieee.std_logic_1164.all;

entity generic_top is
  port (
    clk : in  std_logic;
    d   : in  std_logic_vector(15 downto 0);
    q   : out std_logic_vector(15 downto 0)
  );
end entity generic_top;

architecture struct of generic_top is
  component reg_n is
    generic ( N : integer := 8 );
    port ( clk : in std_logic; d : in std_logic_vector(N-1 downto 0); q : out std_logic_vector(N-1 downto 0) );
  end component;
begin
  u : reg_n generic map ( N => 16 ) port map ( clk => clk, d => d, q => q );
end architecture struct;
