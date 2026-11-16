-- Negative fixture: a `$` character (UNKNOWN token) inside an
-- architecture body must produce a precise parser error pointing
-- AT the unknown token, not be silently skipped.
--
-- Regression guard: TokenStream.Expect previously logged
-- "Unknown token please check it out, skipping for simplicity"
-- and advanced past UNKNOWN tokens — letting garbage like `MHz`/`$0`
-- pasted into a student's source survive the build pre-flight.

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity garbage_body is
  port (
    a   : in  STD_LOGIC;
    o0  : out STD_LOGIC
  );
end garbage_body;

architecture behavioral of garbage_body is
begin
  o0 $ a;
end behavioral;
