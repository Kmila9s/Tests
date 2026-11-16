-- Test 38: 8-input Vectored Interrupt Controller with priority
-- Curriculum: Sistemas en Chip, Unit 2.2

library IEEE;
use IEEE.std_logic_1164.all;
use IEEE.numeric_std.all;

entity InterruptController is
    port (
        clk       : in  std_logic;
        rst       : in  std_logic;
        irq_in    : in  std_logic_vector(7 downto 0);
        irq_mask  : in  std_logic_vector(7 downto 0);
        ack       : in  std_logic;
        irq_out   : out std_logic;
        vector    : out std_logic_vector(2 downto 0);
        pending   : out std_logic_vector(7 downto 0)
    );
end InterruptController;

architecture RTL of InterruptController is
    signal pend_reg : std_logic_vector(7 downto 0) := (others => '0');
    signal masked   : std_logic_vector(7 downto 0);
    signal vec_int  : unsigned(2 downto 0) := (others => '0');
    signal active   : std_logic := '0';
begin
    masked <= pend_reg and irq_mask;

    pend_proc : process(clk, rst)
    begin
        if rst = '1' then
            pend_reg <= (others => '0');
            active   <= '0';
            vec_int  <= (others => '0');
        elsif rising_edge(clk) then
            -- Latch new IRQs
            for i in 0 to 7 loop
                if irq_in(i) = '1' then
                    pend_reg(i) <= '1';
                end if;
            end loop;

            -- Acknowledge clears highest pending
            if ack = '1' and active = '1' then
                pend_reg(to_integer(vec_int)) <= '0';
                active <= '0';
            end if;

            -- Priority encode (highest bit wins)
            active <= '0';
            for i in 7 downto 0 loop
                if masked(i) = '1' and active = '0' then
                    vec_int <= to_unsigned(i, 3);
                    active  <= '1';
                end if;
            end loop;
        end if;
    end process pend_proc;

    irq_out <= active;
    vector  <= std_logic_vector(vec_int);
    pending <= pend_reg;
end RTL;
