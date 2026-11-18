-- G30: call to an undeclared procedure inside a generate body (GHDL: no declaration).
library ieee; use ieee.std_logic_1164.all;
entity undeclared is
  port (n : in std_logic_vector(3 downto 0); y : out std_logic);
end entity;
architecture rtl of undeclared is
begin
  g: for k in 0 to 0 generate
    foo(n(k));
  end generate;
  y <= n(0);
end architecture;
