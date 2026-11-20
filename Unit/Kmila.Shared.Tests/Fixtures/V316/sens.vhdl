-- G62/G67/G72/G73: implicit sensitivity counts only the names in the
-- statement (function bodies are not looked into); process(all) also reads
-- the subprograms it calls; each instance has its own subprograms.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity sens_sub is
  port (a : in std_logic_vector(3 downto 0); kin : in std_logic_vector(3 downto 0);
        o : out std_logic_vector(3 downto 0));
end entity;

architecture rtl of sens_sub is
  signal k_sig : std_logic_vector(3 downto 0);
  function add_off(x : std_logic_vector(3 downto 0)) return std_logic_vector is
  begin
    return std_logic_vector(unsigned(x) + unsigned(k_sig));
  end function;
begin
  k_sig <= kin;
  o <= add_off(a);
end architecture;

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity sens is
  port (a, kin1, kin2 : in std_logic_vector(3 downto 0); c : in std_logic;
        o1, o2, oc, os, oa, op, pc_out : out std_logic_vector(3 downto 0));
end entity;

architecture rtl of sens is
  signal k : std_logic_vector(3 downto 0);
  function plus_k(x : std_logic_vector(3 downto 0)) return std_logic_vector is
  begin
    return std_logic_vector(unsigned(x) + unsigned(k));
  end function;
  procedure copy(signal src : in std_logic_vector(3 downto 0);
                 signal dst : out std_logic_vector(3 downto 0)) is
  begin
    dst <= std_logic_vector(unsigned(src) + unsigned(k));
  end procedure;
begin
  u1: entity work.sens_sub port map (a => a, kin => kin1, o => o1);
  u2: entity work.sens_sub port map (a => a, kin => kin2, o => o2);
  k <= kin1;
  oc <= plus_k(a) when c = '1' else a;
  with c select os <= plus_k(a) when '1', a when others;
  -- process(all): the sensitivity includes k, read inside plus_k (GHDL rule)
  pa: process (all) begin oa <= plus_k(a); end process;
  -- process(a): k changes do not wake it
  pp: process (a) begin op <= plus_k(a); end process;
  -- concurrent procedure call: sensitive to its in actual (a), not to k
  copy(a, pc_out);
end architecture;
