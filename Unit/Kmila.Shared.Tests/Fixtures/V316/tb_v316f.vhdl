-- Testbench for numeric_signed, slv_signed/unsigned/arith, wide, widths,
-- lit10 and concat_arg: four 10 ns phases, samples at t=5/15/25/35.
library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity tb_v316f is
end entity;

architecture sim of tb_v316f is
  signal a, b, addr : std_logic_vector(3 downto 0);
  signal ti : integer range -8 to 7;
  signal rz8, mi, ssx, sex, cy, cz, dd : std_logic_vector(7 downto 0);
  signal rz2 : std_logic_vector(1 downto 0);
  signal sr, sl, ab, ng, dv, md, rm, ss, us, cc : std_logic_vector(3 downto 0);
  signal lt, gt0, eqw, slt, ult : std_logic;
  signal si, ui, i31 : integer;
  signal x40, y40, s40 : std_logic_vector(39 downto 0);
  signal p80 : std_logic_vector(79 downto 0);
  signal lt40 : std_logic;
  signal x64, s64, neg64 : std_logic_vector(63 downto 0);
  signal x96, s96 : std_logic_vector(95 downto 0);
  signal ua, ub, uc : unsigned(3 downto 0);
  signal y8, y9, yi : unsigned(7 downto 0);
begin
  un: entity work.numeric_signed port map (a => a, b => b, ti => ti, rz8 => rz8, rz2 => rz2,
    sr => sr, sl => sl, ab => ab, ng => ng, lt => lt, gt0 => gt0, eqw => eqw, mi => mi,
    dv => dv, md => md, rm => rm);
  u1: entity work.slv_signed port map (a => a, b => b, lt => slt, s => ss, i => si);
  u2: entity work.slv_unsigned port map (a => a, b => b, lt => ult, s => us, i => ui);
  u3: entity work.slv_arith port map (a => a, sx => ssx, ex => sex);
  uw: entity work.wide port map (x40 => x40, y40 => y40, x64 => x64, x96 => x96, s40 => s40,
    p80 => p80, lt40 => lt40, s64 => s64, s96 => s96, neg64 => neg64, i31 => i31);
  ux: entity work.widths port map (a => ua, b => ub, c => uc, y8 => y8, y9 => y9, yi => yi);
  ul: entity work.lit10 port map (addr => addr, d => dd, c => cc);
  ucat: entity work.concat_arg port map (a => a, y => cy, z => cz);
  stim: process
  begin
    a <= "0101"; b <= "0011"; addr <= "0010";
    x40 <= x"00FFFFFFFF"; y40 <= x"0000000001"; x64 <= (others => '1');
    x96 <= x"800000000000000000000001"; ua <= "1111"; ub <= "1111"; uc <= "0001";
    wait for 10 ns;
    a <= "1101"; b <= "0010"; addr <= "1010";
    x40 <= x"8000000000"; y40 <= x"7FFFFFFFFF"; x64 <= x"0000000100000000";
    x96 <= x"0000000F0000000000000000"; ua <= "0011"; ub <= "0100"; uc <= "0101";
    wait for 10 ns;
    a <= "1000"; b <= "1111"; addr <= "1100";
    x40 <= x"7FFFFFFFFF"; y40 <= x"8000000000"; x64 <= x"8000000000000000";
    ua <= "1000"; ub <= "1000"; uc <= "1111";
    wait for 10 ns;
    a <= "0111"; b <= "1110"; addr <= "1110";
    x40 <= x"123456789A"; y40 <= x"0000000010"; x64 <= x"7FFFFFFFFFFFFFFF";
    wait;
  end process;
  mon: process
  begin
    wait for 5 ns;
    for k in 0 to 3 loop
      report "t=" & integer'image(k * 10 + 5)
        & " ns.ti=" & to_string(std_logic_vector(to_signed(ti, 4)))
        & " ns.rz8=" & to_string(rz8) & " ns.rz2=" & to_string(rz2)
        & " ns.sr=" & to_string(sr) & " ns.sl=" & to_string(sl) & " ns.ab=" & to_string(ab)
        & " ns.ng=" & to_string(ng) & " ns.lt=" & std_logic'image(lt)(2)
        & " ns.gt0=" & std_logic'image(gt0)(2) & " ns.eqw=" & std_logic'image(eqw)(2)
        & " ns.mi=" & to_string(mi) & " ns.dv=" & to_string(dv) & " ns.md=" & to_string(md)
        & " ns.rm=" & to_string(rm)
        & " sg.lt=" & std_logic'image(slt)(2) & " sg.s=" & to_string(ss)
        & " sg.i=" & to_string(std_logic_vector(to_signed(si, 8)))
        & " us.lt=" & std_logic'image(ult)(2) & " us.s=" & to_string(us)
        & " us.i=" & to_string(std_logic_vector(to_signed(ui, 8)))
        & " ar.sx=" & to_string(ssx) & " ar.ex=" & to_string(sex)
        & " wd.s40=" & to_string(s40) & " wd.p80=" & to_string(p80)
        & " wd.lt40=" & std_logic'image(lt40)(2) & " wd.s64=" & to_string(s64)
        & " wd.s96=" & to_string(s96) & " wd.neg64=" & to_string(neg64)
        & " wd.i31=" & to_string(std_logic_vector(to_unsigned(i31, 31)))
        & " wi.y8=" & to_string(y8) & " wi.y9=" & to_string(y9) & " wi.yi=" & to_string(yi)
        & " lt10.d=" & to_string(dd) & " lt10.c=" & to_string(cc)
        & " ca.y=" & to_string(cy) & " ca.z=" & to_string(cz);
      wait for 10 ns;
    end loop;
    wait;
  end process;
end architecture;
