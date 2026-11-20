-- v3.1.5 G58: string objects and character comparison.
library ieee;
use ieee.std_logic_1164.all;

entity strings_g58 is
  port (
    n : in std_logic;
    ceq, seq, cat, len3, lex : out std_logic
  );
end entity;

architecture rtl of strings_g58 is
begin
  process (n)
    variable s : string(1 to 3);
    variable t : string(1 to 4);
    variable ch : character;
  begin
    if n = '1' then
      s := "abc";
    else
      s := "abd";
    end if;
    ch := s(2);
    t := s & "z";
    report "g58: s=" & s & " t=" & t;
    if ch = 'b' then ceq <= '1'; else ceq <= '0'; end if;
    if s = "abc" then seq <= '1'; else seq <= '0'; end if;
    if t(4) = 'z' then cat <= '1'; else cat <= '0'; end if;
    if s'length = 3 then len3 <= '1'; else len3 <= '0'; end if;
    if s < "abd" then lex <= '1'; else lex <= '0'; end if;
  end process;
end architecture;
