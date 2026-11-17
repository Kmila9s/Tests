-- G09/G13: record fields of integer, constrained integer, enum and boolean type.
library ieee; use ieee.std_logic_1164.all; use ieee.numeric_std.all;
entity rec_int_enum is
  port (a : in std_logic; n : in std_logic_vector(3 downto 0);
        y : out std_logic_vector(3 downto 0); z : out std_logic_vector(4 downto 0);
        st : out std_logic; f : out std_logic);
end entity;
architecture rtl of rec_int_enum is
  type state_t is (idle, run, done);
  type ctl_t is record
    cnt : integer;
    lim : integer range 0 to 15;
    s   : state_t;
    ok  : boolean;
  end record;
  signal c : ctl_t := (cnt => 0, lim => 0, s => idle, ok => false);
begin
  c.cnt <= to_integer(unsigned(n)) + 1;
  c.lim <= to_integer(unsigned(n));
  c.s   <= run when a = '1' else done;
  c.ok  <= a = '1';
  y  <= std_logic_vector(to_unsigned(c.lim, 4));
  z  <= std_logic_vector(to_unsigned(c.cnt, 5));
  st <= '1' when c.s = run else '0';
  f  <= '1' when c.ok else '0';
end architecture;
