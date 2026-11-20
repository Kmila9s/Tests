entity ovf_probe is end;
architecture a of ovf_probe is
  signal go : bit := '0';
begin
  go <= '1' after 10 ns;
  process (go)
    variable v : integer := 2147483647;
  begin
    if go = '1' then v := v + 1; end if;
    report "v=" & integer'image(v) & " high=" & integer'image(integer'high);
  end process;
end;
