-- Kitchen-sink round-trip fixture (2026-07-06).
-- Purpose-built to exercise every currently-open F-* gap in one file so
-- the round-trip audit reports per-construct raw counts. Deliberately
-- hostile — most raws are EXPECTED here and asserted individually.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity kitchen_sink is
  port (
    clk    : in  std_logic;
    reset  : in  std_logic;
    en     : in  std_logic;
    cnt_in : in  std_logic_vector(7 downto 0);
    y      : out std_logic;
    q      : out std_logic_vector(7 downto 0)
  );
end entity kitchen_sink;

architecture behavioral of kitchen_sink is
  -- Signal with aggregate initializer (F-18 gap: init currently dropped).
  signal r    : std_logic_vector(7 downto 0) := (others => '0');
  signal ucnt : unsigned(7 downto 0) := (others => '0');
begin
  -- Unlabeled process — the classic shape.
  process (clk, reset)
  begin
    if reset = '1' then
      r    <= (others => '0');
      ucnt <= (others => '0');
    elsif rising_edge(clk) then
      if en = '1' then
        -- Arithmetic with integer literal (F-9 clean).
        ucnt <= ucnt + 1;
        -- Concatenation (F-9 clean).
        r <= r(6 downto 0) & cnt_in(0);
        -- Case with vector-literal choices (F-4 clean).
        case cnt_in(1 downto 0) is
          when "00"   => y <= '0';
          when "01"   => y <= '1';
          when others => y <= 'X';
        end case;
      end if;
    end if;
  end process;

  -- Labeled process (F-11 gap: label currently dropped → raw fallback).
  p_out: process (r)
  begin
    q <= r;
  end process p_out;

  -- Conditional concurrent signal assignment (F-N gap: entire line → raw).
  y <= '1' when ucnt = 5 else '0';

  -- Function call / type conversion (F-N gap: currently mis-parsed).
  q <= std_logic_vector(ucnt);

  -- Component instantiation (F-11 gap: raw_concurrent).
  u1: some_component port map (a => clk, b => reset);

  -- For-generate (F-N gap: raw_concurrent for whole block).
  gen: for i in 0 to 7 generate
    q(i) <= r(i);
  end generate gen;
end architecture behavioral;
