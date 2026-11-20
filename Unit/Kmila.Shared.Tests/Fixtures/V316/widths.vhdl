-- G68: numeric_std result widths in chained expressions, and a whole
-- assignment with a different length (run-time error).
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity widths is
  port (a, b, c : in unsigned(3 downto 0);
        y8 : out unsigned(7 downto 0);   -- a*b + c is 8 bits
        y9 : out unsigned(7 downto 0);   -- (a+b)*c is 8 bits
        yi : out unsigned(7 downto 0));  -- a * 2 is 8 bits (2L)
end entity;

architecture rtl of widths is
begin
  y8 <= a * b + c;
  y9 <= (a + b) * c;
  yi <= a * 2;
end architecture;

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity len_err is
  port (a : in unsigned(3 downto 0); y : out unsigned(3 downto 0));
end entity;

architecture rtl of len_err is
begin
  y <= a + a + "00000";   -- 5 bits into 4: length error at run time
end architecture;
