library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

-- Independent implementation. Uses numeric_std instead of the deprecated
-- std_logic_unsigned package the reference relies on. Functional contract
-- (entity ports, reset polarity, edge sensitivity, counting direction)
-- matches the reference.
entity counter is
    port (
        clk   : in  std_logic;
        n_rst : in  std_logic;
        cout  : out std_logic_vector(7 downto 0)
    );
end counter;

architecture rtl of counter is
    signal cnt : unsigned(7 downto 0) := (others => '0');
begin
    process(clk, n_rst)
    begin
        if n_rst = '0' then
            cnt <= (others => '0');
        elsif rising_edge(clk) then
            cnt <= cnt + 1;
        end if;
    end process;

    cout <= std_logic_vector(cnt);
end rtl;
